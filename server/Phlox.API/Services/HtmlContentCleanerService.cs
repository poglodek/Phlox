using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using Phlox.API.Configuration;

namespace Phlox.API.Services;

public partial class HtmlContentCleanerService : IHtmlContentCleanerService
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<HtmlContentCleanerService> _logger;

    public HtmlContentCleanerService(
        IOptions<OpenAiOptions> options,
        ILogger<HtmlContentCleanerService> logger)
    {
        _logger = logger;

        // Use GPT-4o-mini for vision tasks
        _chatClient = new ChatClient("gpt-4o-mini", options.Value.ApiKey);

        _logger.LogInformation("HtmlContentCleanerService initialized with GPT-4o-mini for image descriptions");
    }

    public async Task<HtmlCleaningResult> CleanHtmlAsync(string htmlContent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(htmlContent))
        {
            _logger.LogWarning("Empty HTML content provided for cleaning");
            return new HtmlCleaningResult { CleanedText = string.Empty };
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(htmlContent);

        // Extract images before cleaning
        var images = ExtractImages(doc);
        _logger.LogInformation("Found {ImageCount} images in HTML content", images.Count);

        // Clean the HTML and extract text
        var cleanedText = ExtractTextContent(doc);

        // Process images with AI vision
        var imageDescriptions = await DescribeImagesAsync(images, cancellationToken);

        return new HtmlCleaningResult
        {
            CleanedText = cleanedText,
            ImageDescriptions = imageDescriptions
        };
    }

    private List<ExtractedImage> ExtractImages(HtmlDocument doc)
    {
        var images = new List<ExtractedImage>();
        var imgNodes = doc.DocumentNode.SelectNodes("//img");

        if (imgNodes == null)
        {
            return images;
        }

        var position = 0;
        foreach (var imgNode in imgNodes)
        {
            var src = imgNode.GetAttributeValue("src", null);
            var alt = imgNode.GetAttributeValue("alt", null);

            if (!string.IsNullOrWhiteSpace(src))
            {
                images.Add(new ExtractedImage
                {
                    Source = src,
                    AltText = alt,
                    Position = position++
                });
            }
        }

        return images;
    }

    private string ExtractTextContent(HtmlDocument doc)
    {
        // Remove script and style elements
        var nodesToRemove = doc.DocumentNode.SelectNodes("//script|//style|//noscript|//head|//meta|//link");
        if (nodesToRemove != null)
        {
            foreach (var node in nodesToRemove.ToList())
            {
                node.Remove();
            }
        }

        // Get text content
        var textBuilder = new StringBuilder();
        ExtractTextFromNode(doc.DocumentNode, textBuilder);

        var text = textBuilder.ToString();

        // Clean up the text
        text = WebUtility.HtmlDecode(text);
        text = MultipleWhitespaceRegex().Replace(text, " ");
        text = MultipleNewlinesRegex().Replace(text, "\n\n");
        text = text.Trim();

        return text;
    }

    private static void ExtractTextFromNode(HtmlNode node, StringBuilder textBuilder)
    {
        if (node.NodeType == HtmlNodeType.Text)
        {
            var text = node.InnerText;
            if (!string.IsNullOrWhiteSpace(text))
            {
                textBuilder.Append(text);
                textBuilder.Append(' ');
            }
            return;
        }

        // Add line breaks for block elements
        var blockElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "p", "div", "br", "h1", "h2", "h3", "h4", "h5", "h6",
            "li", "tr", "article", "section", "header", "footer",
            "blockquote", "pre", "hr"
        };

        if (blockElements.Contains(node.Name))
        {
            textBuilder.AppendLine();
        }

        foreach (var child in node.ChildNodes)
        {
            ExtractTextFromNode(child, textBuilder);
        }

        if (blockElements.Contains(node.Name))
        {
            textBuilder.AppendLine();
        }
    }

    private async Task<List<ImageDescription>> DescribeImagesAsync(
        List<ExtractedImage> images,
        CancellationToken cancellationToken)
    {
        var descriptions = new List<ImageDescription>();

        foreach (var image in images)
        {
            try
            {
                var description = await DescribeImageAsync(image, cancellationToken);
                descriptions.Add(description);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to describe image at position {Position}: {Source}",
                    image.Position, TruncateSource(image.Source));

                // Add a fallback description
                descriptions.Add(new ImageDescription
                {
                    Source = image.Source,
                    AltText = image.AltText,
                    Description = image.AltText ?? "Image could not be described",
                    Position = image.Position
                });
            }
        }

        return descriptions;
    }

    private async Task<ImageDescription> DescribeImageAsync(ExtractedImage image, CancellationToken cancellationToken)
    {
        ChatMessageContentPart imagePart;

        if (image.Source.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
        {
            // Base64 encoded image
            imagePart = CreateBase64ImagePart(image.Source);
        }
        else if (Uri.TryCreate(image.Source, UriKind.Absolute, out var imageUri))
        {
            // URL-based image
            imagePart = ChatMessageContentPart.CreateImagePart(imageUri);
        }
        else
        {
            // Invalid image source, use alt text as fallback
            _logger.LogDebug("Invalid image source, using alt text: {Source}", TruncateSource(image.Source));

            return new ImageDescription
            {
                Source = image.Source,
                AltText = image.AltText,
                Description = image.AltText ?? "Invalid image source",
                Position = image.Position
            };
        }

        var messages = new List<ChatMessage>
        {
            new UserChatMessage(
                ChatMessageContentPart.CreateTextPart(
                    "Describe this image concisely in 1-2 sentences. Focus on the main subject and key visual elements. " +
                    "If it's a diagram, chart, or technical image, describe what it represents."),
                imagePart)
        };

        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = 150
        };

        var response = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);
        var description = response.Value.Content.FirstOrDefault()?.Text ?? "No description available";

        _logger.LogDebug("Generated description for image at position {Position}: {Description}",
            image.Position, description);

        return new ImageDescription
        {
            Source = image.Source,
            AltText = image.AltText,
            Description = description,
            Position = image.Position
        };
    }

    private static ChatMessageContentPart CreateBase64ImagePart(string dataUri)
    {
        // Parse data URI: data:image/png;base64,<data>
        var match = DataUriRegex().Match(dataUri);

        if (!match.Success)
        {
            throw new ArgumentException("Invalid data URI format", nameof(dataUri));
        }

        var mimeType = match.Groups[1].Value;
        var base64Data = match.Groups[2].Value;
        var imageBytes = Convert.FromBase64String(base64Data);

        return ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(imageBytes), mimeType);
    }

    private static string TruncateSource(string source)
    {
        const int maxLength = 100;
        return source.Length > maxLength ? source[..maxLength] + "..." : source;
    }

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultipleWhitespaceRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultipleNewlinesRegex();

    [GeneratedRegex(@"^data:(image/[^;]+);base64,(.+)$", RegexOptions.Singleline)]
    private static partial Regex DataUriRegex();

    private class ExtractedImage
    {
        public required string Source { get; set; }
        public string? AltText { get; set; }
        public int Position { get; set; }
    }
}
