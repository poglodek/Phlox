using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using Phlox.API.Configuration;

namespace Phlox.API.Services;

public class DocumentSlicerService : IDocumentSlicerService, IDisposable
{
    private readonly InferenceSession _session;
    private readonly SentencePieceTokenizer _tokenizer;
    private readonly ILogger<DocumentSlicerService> _logger;
    private readonly float _threshold;
    private readonly int _maxLength;
    private bool _disposed;

    public DocumentSlicerService(
        IOptions<DocumentSlicerOptions> options,
        ILogger<DocumentSlicerService> logger)
    {
        _logger = logger;
        _threshold = options.Value.Threshold;
        _maxLength = options.Value.MaxLength;

        var modelPath = options.Value.ModelPath;
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"SAT model not found at path: {modelPath}");
        }

        var tokenizerPath = options.Value.TokenizerPath;
        if (!File.Exists(tokenizerPath))
        {
            throw new FileNotFoundException($"Tokenizer model not found at path: {tokenizerPath}");
        }

        _session = new InferenceSession(modelPath);

        // Load SentencePiece tokenizer for xlm-roberta-base
        using var tokenizerStream = File.OpenRead(tokenizerPath);
        _tokenizer = SentencePieceTokenizer.Create(tokenizerStream, true, false, null);

        _logger.LogInformation("DocumentSlicerService initialized with model: {ModelPath}", modelPath);
    }

    public List<string> SliceIntoParagraphs(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("Empty content provided for slicing");
            return [];
        }

        // Split content into sentences first (simple split by sentence boundaries)
        var sentences = SplitIntoSentences(content);

        if (sentences.Count <= 1)
        {
            return [content.Trim()];
        }

        // Process sentences and use the model to predict paragraph boundaries
        var paragraphs = new List<string>();
        var currentParagraph = new List<string>();

        for (var i = 0; i < sentences.Count; i++)
        {
            currentParagraph.Add(sentences[i]);

            // Check if this is a paragraph boundary using the model
            if (i < sentences.Count - 1)
            {
                var context = string.Join(" ", currentParagraph.TakeLast(3));
                var nextSentence = sentences[i + 1];

                var isParagraphBreak = PredictParagraphBreak(context, nextSentence);

                if (isParagraphBreak)
                {
                    paragraphs.Add(string.Join(" ", currentParagraph).Trim());
                    currentParagraph.Clear();
                }
            }
        }

        // Add remaining sentences as last paragraph
        if (currentParagraph.Count > 0)
        {
            paragraphs.Add(string.Join(" ", currentParagraph).Trim());
        }

        _logger.LogInformation("Sliced document into {ParagraphCount} paragraphs", paragraphs.Count);
        return paragraphs;
    }

    private bool PredictParagraphBreak(string context, string nextSentence)
    {
        var inputText = $"{context} {nextSentence}";

        var encoding = _tokenizer.EncodeToIds(inputText, _maxLength, out _, out _);
        var tokenCount = encoding.Count;

        var inputIds = new DenseTensor<long>(new[] { 1, tokenCount });
        var attentionMask = new DenseTensor<long>(new[] { 1, tokenCount });

        for (var i = 0; i < tokenCount; i++)
        {
            inputIds[0, i] = encoding[i];
            attentionMask[0, i] = 1;
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask)
        };

        using var results = _session.Run(inputs);
        var logits = results.First().AsTensor<float>();

        // The model outputs logits for each token - we look at the boundary position
        // Find the position where context ends and next sentence begins
        var contextTokens = _tokenizer.EncodeToIds(context, _maxLength, out _, out _);
        var boundaryPosition = Math.Min(contextTokens.Count, tokenCount - 1);

        // Apply sigmoid to get probability at the boundary position
        var logit = logits[0, boundaryPosition, 0];
        var probability = 1.0f / (1.0f + (float)Math.Exp(-logit));

        return probability > _threshold;
    }

    private static List<string> SplitIntoSentences(string content)
    {
        var sentences = new List<string>();
        var sentenceEnders = new[] { ". ", "! ", "? ", ".\n", "!\n", "?\n", ".\r\n", "!\r\n", "?\r\n" };
        var currentStart = 0;

        for (var i = 0; i < content.Length - 1; i++)
        {
            foreach (var ender in sentenceEnders)
            {
                if (i + ender.Length <= content.Length &&
                    content.Substring(i, ender.Length) == ender)
                {
                    var sentence = content.Substring(currentStart, i - currentStart + 1).Trim();
                    if (!string.IsNullOrWhiteSpace(sentence))
                    {
                        sentences.Add(sentence);
                    }
                    currentStart = i + ender.Length;
                    break;
                }
            }
        }

        // Add remaining text as last sentence
        if (currentStart < content.Length)
        {
            var lastSentence = content[currentStart..].Trim();
            if (!string.IsNullOrWhiteSpace(lastSentence))
            {
                sentences.Add(lastSentence);
            }
        }

        return sentences.Count > 0 ? sentences : [content.Trim()];
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _session.Dispose();
        }

        _disposed = true;
    }
}
