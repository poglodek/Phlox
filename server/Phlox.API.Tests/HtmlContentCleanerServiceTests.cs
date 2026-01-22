using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Phlox.API.Configuration;
using Phlox.API.Services;

namespace Phlox.API.Tests;

public class HtmlContentCleanerServiceTests : IDisposable
{
    private readonly HtmlContentCleanerService _sut;
    private readonly ILogger<HtmlContentCleanerService> _logger;

    public HtmlContentCleanerServiceTests()
    {
        _logger = NullLogger<HtmlContentCleanerService>.Instance;

        var options = Options.Create(new OpenAiOptions
        {
            ApiKey = "test-api-key",
            Model = "text-embedding-3-small",
            Dimensions = 1536
        });


        _sut = new HtmlContentCleanerService(options, _logger);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task CleanHtmlAsync_WithEmptyContent_ReturnsEmptyResult()
    {
        // Arrange
        var content = "";

        // Act
        var result = await _sut.CleanHtmlAsync(content);

        // Assert
        Assert.Empty(result.CleanedText);
        Assert.Empty(result.ImageDescriptions);
    }

    [Fact]
    public async Task CleanHtmlAsync_WithWhitespaceContent_ReturnsEmptyResult()
    {
        // Arrange
        var content = "   \n\t  ";

        // Act
        var result = await _sut.CleanHtmlAsync(content);

        // Assert
        Assert.Empty(result.CleanedText);
        Assert.Empty(result.ImageDescriptions);
    }

    [Fact]
    public async Task CleanHtmlAsync_WithPlainText_ReturnsTextUnchanged()
    {
        // Arrange
        var content = "This is plain text without any HTML.";

        // Act
        var result = await _sut.CleanHtmlAsync(content);

        // Assert
        Assert.Contains("This is plain text without any HTML", result.CleanedText);
        Assert.Empty(result.ImageDescriptions);
    }

    [Fact]
    public async Task CleanHtmlAsync_WithSimpleHtml_StripsTagsAndReturnsText()
    {
        // Arrange
        var content = "<p>Hello <strong>World</strong>!</p>";

        // Act
        var result = await _sut.CleanHtmlAsync(content);

        // Assert
        Assert.Contains("Hello", result.CleanedText);
        Assert.Contains("World", result.CleanedText);
        Assert.DoesNotContain("<p>", result.CleanedText);
        Assert.DoesNotContain("<strong>", result.CleanedText);
    }

    [Fact]
    public async Task CleanHtmlAsync_WithNestedHtml_ExtractsAllText()
    {
        // Arrange
        var content = @"
            <html>
            <body>
                <div>
                    <h1>Title</h1>
                    <p>First paragraph</p>
                    <p>Second paragraph</p>
                </div>
            </body>
            </html>";

        // Act
        var result = await _sut.CleanHtmlAsync(content);

        // Assert
        Assert.Contains("Title", result.CleanedText);
        Assert.Contains("First paragraph", result.CleanedText);
        Assert.Contains("Second paragraph", result.CleanedText);
    }

    [Fact]
    public async Task CleanHtmlAsync_WithScriptAndStyleTags_RemovesThemCompletely()
    {
        // Arrange
        var content = @"
            <html>
            <head>
                <style>body { color: red; }</style>
            </head>
            <body>
                <script>alert('hello');</script>
                <p>Visible content</p>
            </body>
            </html>";

        // Act
        var result = await _sut.CleanHtmlAsync(content);

        // Assert
        Assert.Contains("Visible content", result.CleanedText);
        Assert.DoesNotContain("alert", result.CleanedText);
        Assert.DoesNotContain("color: red", result.CleanedText);
    }

    [Fact]
    public async Task CleanHtmlAsync_WithHtmlEntities_DecodesEntities()
    {
        // Arrange
        var content = "<p>Less than: &lt; Greater than: &gt; Ampersand: &amp;</p>";

        // Act
        var result = await _sut.CleanHtmlAsync(content);

        // Assert
        Assert.Contains("Less than: <", result.CleanedText);
        Assert.Contains("Greater than: >", result.CleanedText);
        Assert.Contains("Ampersand: &", result.CleanedText);
    }

    [Fact]
    public async Task CleanHtmlAsync_WithImageTag_ExtractsImageSource()
    {
        // Arrange
        var content = @"
            <div>
                <p>Some text</p>
                <img src=""https://example.com/image.jpg"" alt=""Test image"" />
            </div>";

        // Act
        var result = await _sut.CleanHtmlAsync(content);

        // Assert
        Assert.Contains("Some text", result.CleanedText);
        Assert.Single(result.ImageDescriptions);
        Assert.Equal("https://example.com/image.jpg", result.ImageDescriptions[0].Source);
        Assert.Equal("Test image", result.ImageDescriptions[0].AltText);
    }

    [Fact]
    public async Task CleanHtmlAsync_WithMultipleImages_ExtractsAllImages()
    {
        // Arrange
        var content = @"
            <div>
                <img src=""https://example.com/image1.jpg"" alt=""First"" />
                <img src=""https://example.com/image2.jpg"" alt=""Second"" />
                <img src=""https://example.com/image3.jpg"" />
            </div>";

        // Act
        var result = await _sut.CleanHtmlAsync(content);

        // Assert
        Assert.Equal(3, result.ImageDescriptions.Count);
        Assert.Equal("https://example.com/image1.jpg", result.ImageDescriptions[0].Source);
        Assert.Equal("https://example.com/image2.jpg", result.ImageDescriptions[1].Source);
        Assert.Equal("https://example.com/image3.jpg", result.ImageDescriptions[2].Source);
        Assert.Equal("First", result.ImageDescriptions[0].AltText);
        Assert.Equal("Second", result.ImageDescriptions[1].AltText);
        Assert.Null(result.ImageDescriptions[2].AltText);
    }

    [Fact]
    public async Task CleanHtmlAsync_WithImageWithoutSrc_IgnoresImage()
    {
        // Arrange
        var content = @"<div><img alt=""No source"" /></div>";

        // Act
        var result = await _sut.CleanHtmlAsync(content);

        // Assert
        Assert.Empty(result.ImageDescriptions);
    }

    [Fact]
    public async Task CleanHtmlAsync_PreservesImageOrder()
    {
        // Arrange
        var content = @"
            <div>
                <img src=""image1.jpg"" />
                <p>text</p>
                <img src=""image2.jpg"" />
                <img src=""image3.jpg"" />
            </div>";

        // Act
        var result = await _sut.CleanHtmlAsync(content);

        // Assert
        Assert.Equal(0, result.ImageDescriptions[0].Position);
        Assert.Equal(1, result.ImageDescriptions[1].Position);
        Assert.Equal(2, result.ImageDescriptions[2].Position);
    }

    [Fact]
    public async Task CleanHtmlAsync_WithComplexHtml_CleansCorrectly()
    {
        // Arrange
        var content = @"
            <!DOCTYPE html>
            <html lang=""en"">
            <head>
                <meta charset=""UTF-8"">
                <title>Test Document</title>
                <style>.hidden { display: none; }</style>
            </head>
            <body>
                <header>
                    <nav>Navigation</nav>
                </header>
                <main>
                    <article>
                        <h1>Article Title</h1>
                        <p>First paragraph with some <a href=""#"">link text</a>.</p>
                        <img src=""https://example.com/photo.jpg"" alt=""A photo"" />
                        <p>Second paragraph.</p>
                    </article>
                </main>
                <footer>Footer content</footer>
                <script>console.log('loaded');</script>
            </body>
            </html>";

        // Act
        var result = await _sut.CleanHtmlAsync(content);

        // Assert
        Assert.Contains("Navigation", result.CleanedText);
        Assert.Contains("Article Title", result.CleanedText);
        Assert.Contains("First paragraph", result.CleanedText);
        Assert.Contains("link text", result.CleanedText);
        Assert.Contains("Second paragraph", result.CleanedText);
        Assert.Contains("Footer content", result.CleanedText);
        Assert.DoesNotContain("console.log", result.CleanedText);
        Assert.DoesNotContain("display: none", result.CleanedText);
        Assert.Single(result.ImageDescriptions);
    }

    [Fact]
    public void GetAllContentParagraphs_WithTextAndImages_CombinesCorrectly()
    {
        // Arrange
        var result = new HtmlCleaningResult
        {
            CleanedText = "Main text content",
            ImageDescriptions =
            [
                new ImageDescription
                {
                    Source = "img1.jpg",
                    Description = "A cat sitting on a couch",
                    Position = 0
                },
                new ImageDescription
                {
                    Source = "img2.jpg",
                    Description = "A scenic mountain view",
                    Position = 1
                }
            ]
        };

        // Act
        var paragraphs = result.GetAllContentParagraphs();

        // Assert
        Assert.Equal(3, paragraphs.Count);
        Assert.Equal("Main text content", paragraphs[0]);
        Assert.Contains("A cat sitting on a couch", paragraphs[1]);
        Assert.Contains("A scenic mountain view", paragraphs[2]);
    }

    [Fact]
    public void GetAllContentParagraphs_WithEmptyText_ReturnsOnlyImageDescriptions()
    {
        // Arrange
        var result = new HtmlCleaningResult
        {
            CleanedText = "",
            ImageDescriptions =
            [
                new ImageDescription
                {
                    Source = "img.jpg",
                    Description = "An image",
                    Position = 0
                }
            ]
        };

        // Act
        var paragraphs = result.GetAllContentParagraphs();

        // Assert
        Assert.Single(paragraphs);
        Assert.Contains("An image", paragraphs[0]);
    }

    [Fact]
    public void GetAllContentParagraphs_WithNoImages_ReturnsOnlyText()
    {
        // Arrange
        var result = new HtmlCleaningResult
        {
            CleanedText = "Only text here",
            ImageDescriptions = []
        };

        // Act
        var paragraphs = result.GetAllContentParagraphs();

        // Assert
        Assert.Single(paragraphs);
        Assert.Equal("Only text here", paragraphs[0]);
    }

    [Fact]
    public async Task CleanHtmlAsync_WithUnicodeContent_PreservesUnicode()
    {
        // Arrange
        var content = @"<p>Polski tekst z polskimi znakami: ą, ę, ć, ł, ń, ó, ś, ź, ż</p>
                        <p>日本語テキスト</p>
                        <p>Emoji: 🎉 🚀 ❤️</p>";

        // Act
        var result = await _sut.CleanHtmlAsync(content);

        // Assert
        Assert.Contains("Polski tekst", result.CleanedText);
        Assert.Contains("ą, ę, ć, ł, ń, ó, ś, ź, ż", result.CleanedText);
        Assert.Contains("日本語テキスト", result.CleanedText);
        Assert.Contains("🎉", result.CleanedText);
    }

    [Fact]
    public async Task CleanHtmlAsync_WithTableContent_ExtractsText()
    {
        // Arrange
        var content = @"
            <table>
                <tr><th>Header 1</th><th>Header 2</th></tr>
                <tr><td>Cell 1</td><td>Cell 2</td></tr>
                <tr><td>Cell 3</td><td>Cell 4</td></tr>
            </table>";

        // Act
        var result = await _sut.CleanHtmlAsync(content);

        // Assert
        Assert.Contains("Header 1", result.CleanedText);
        Assert.Contains("Header 2", result.CleanedText);
        Assert.Contains("Cell 1", result.CleanedText);
        Assert.Contains("Cell 4", result.CleanedText);
    }

    [Fact]
    public async Task CleanHtmlAsync_WithListContent_ExtractsText()
    {
        // Arrange
        var content = @"
            <ul>
                <li>Item 1</li>
                <li>Item 2</li>
                <li>Item 3</li>
            </ul>";

        // Act
        var result = await _sut.CleanHtmlAsync(content);

        // Assert
        Assert.Contains("Item 1", result.CleanedText);
        Assert.Contains("Item 2", result.CleanedText);
        Assert.Contains("Item 3", result.CleanedText);
    }
}
