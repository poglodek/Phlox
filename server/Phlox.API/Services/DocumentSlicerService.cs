using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using Phlox.API.Configuration;

namespace Phlox.API.Services;

public partial class DocumentSlicerService : IDocumentSlicerService, IDisposable
{
    private readonly ILogger<DocumentSlicerService> _logger;
    private readonly InferenceSession _session;
    private readonly SentencePieceTokenizer _tokenizer;
    private readonly int _maxLength;
    private readonly float _threshold;
    private readonly int _minChunkSize;
    private bool _disposed;

    public DocumentSlicerService(
        IOptions<DocumentSlicerOptions> options,
        ILogger<DocumentSlicerService> logger)
    {
        _logger = logger;
        _maxLength = options.Value.MaxLength;
        _threshold = options.Value.Threshold;
        _minChunkSize = 100;

        var modelPath = options.Value.ModelPath;
        var tokenizerPath = options.Value.TokenizerPath;

        _session = new InferenceSession(modelPath);

        using var tokenizerStream = File.OpenRead(tokenizerPath);
        _tokenizer = SentencePieceTokenizer.Create(tokenizerStream);

        _logger.LogInformation(
            "DocumentSlicerService initialized with ONNX model: {ModelPath}, Tokenizer: {TokenizerPath}",
            modelPath, tokenizerPath);
    }

    public List<string> SliceIntoParagraphs(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("Empty content provided for slicing");
            return [];
        }

        // First, split by existing paragraph breaks (double newlines)
        var rawParagraphs = ParagraphSplitRegex().Split(content)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        var result = new List<string>();

        foreach (var paragraph in rawParagraphs)
        {
            var chunks = SliceWithOnnxModel(paragraph);
            result.AddRange(chunks);
        }

        // Merge very small consecutive paragraphs
        result = MergeSmallParagraphs(result);

        _logger.LogInformation("Sliced document into {ParagraphCount} paragraphs", result.Count);
        return result;
    }

    private List<string> SliceWithOnnxModel(string text)
    {
        var tokens = _tokenizer.EncodeToTokens(text, out _);

        if (tokens.Count == 0)
        {
            return string.IsNullOrWhiteSpace(text) ? [] : [text.Trim()];
        }

        // Process in chunks if text is too long
        if (tokens.Count > _maxLength)
        {
            return SliceLongTextWithOnnx(text, tokens);
        }

        var boundaries = PredictBoundaries(tokens);
        return SplitTextAtBoundaries(text, tokens, boundaries);
    }

    private List<string> SliceLongTextWithOnnx(string text, IReadOnlyList<EncodedToken> tokens)
    {
        var result = new List<string>();
        var tokenIndex = 0;

        while (tokenIndex < tokens.Count)
        {
            var chunkEnd = Math.Min(tokenIndex + _maxLength, tokens.Count);
            var chunkTokens = tokens.Skip(tokenIndex).Take(chunkEnd - tokenIndex).ToList();

            var boundaries = PredictBoundaries(chunkTokens);

            // Find the text range for this chunk with bounds checking
            var chunkStartOffset = Math.Min(tokens[tokenIndex].Offset.Start.Value, text.Length);
            var chunkEndOffset = Math.Min(tokens[chunkEnd - 1].Offset.End.Value, text.Length);

            // Ensure valid substring range
            if (chunkEndOffset <= chunkStartOffset)
            {
                tokenIndex = chunkEnd;
                continue;
            }

            var chunkText = text.Substring(chunkStartOffset, chunkEndOffset - chunkStartOffset);

            if (boundaries.Count > 0)
            {
                var chunks = SplitTextAtBoundaries(chunkText, chunkTokens, boundaries);
                result.AddRange(chunks);
            }
            else if (!string.IsNullOrWhiteSpace(chunkText))
            {
                result.Add(chunkText.Trim());
            }

            tokenIndex = chunkEnd;
        }

        return result;
    }

    private List<int> PredictBoundaries(IReadOnlyList<EncodedToken> tokens)
    {
        // Get model input metadata to determine correct tensor types
        var inputMeta = _session.InputMetadata;

        var inputs = new List<NamedOnnxValue>();

        foreach (var inputName in inputMeta.Keys)
        {
            var meta = inputMeta[inputName];
            var shape = new[] { 1, tokens.Count };

            if (inputName == "input_ids" || inputName.Contains("input"))
            {
                inputs.Add(CreateInputTensor(inputName, tokens.Select(t => t.Id).ToArray(), shape, meta.ElementDataType));
            }
            else if (inputName == "attention_mask" || inputName.Contains("mask"))
            {
                var maskValues = Enumerable.Repeat(1, tokens.Count).ToArray();
                inputs.Add(CreateInputTensor(inputName, maskValues, shape, meta.ElementDataType));
            }
        }

        using var results = _session.Run(inputs);
        var outputMeta = _session.OutputMetadata.First();
        var output = results.First();

        _logger.LogDebug("Model output '{Name}' type: {Type}, Dimensions: [{Dims}]",
            outputMeta.Key,
            outputMeta.Value.ElementDataType,
            string.Join(", ", outputMeta.Value.Dimensions));

        var boundaries = new List<int>();

        // Handle different output formats
        if (outputMeta.Value.ElementDataType == TensorElementType.Float)
        {
            var tensor = output.AsTensor<float>();
            ProcessOutputTensor(tensor, tokens.Count, boundaries);
        }
        else if (outputMeta.Value.ElementDataType == TensorElementType.Float16)
        {
            var tensor = output.AsTensor<Float16>();
            ProcessOutputTensorFloat16(tensor, tokens.Count, boundaries);
        }
        else
        {
            _logger.LogWarning("Unsupported output type: {Type}", outputMeta.Value.ElementDataType);
        }

        return boundaries;
    }

    private static NamedOnnxValue CreateInputTensor(string name, int[] values, int[] shape, TensorElementType elementType)
    {
        return elementType switch
        {
            TensorElementType.Int64 => NamedOnnxValue.CreateFromTensor(name,
                new DenseTensor<long>(values.Select(v => (long)v).ToArray(), shape)),
            TensorElementType.Int32 => NamedOnnxValue.CreateFromTensor(name,
                new DenseTensor<int>(values, shape)),
            TensorElementType.Float16 => NamedOnnxValue.CreateFromTensor(name,
                new DenseTensor<Float16>(values.Select(v => (Float16)v).ToArray(), shape)),
            TensorElementType.Float => NamedOnnxValue.CreateFromTensor(name,
                new DenseTensor<float>(values.Select(v => (float)v).ToArray(), shape)),
            _ => NamedOnnxValue.CreateFromTensor(name,
                new DenseTensor<long>(values.Select(v => (long)v).ToArray(), shape))
        };
    }

    private void ProcessOutputTensor(Tensor<float> tensor, int tokenCount, List<int> boundaries)
    {
        var dimensions = tensor.Dimensions.ToArray();
        var totalElements = (int)tensor.Length;

        _logger.LogDebug("Float tensor shape: [{Dims}], Total elements: {Total}, TokenCount: {Tokens}",
            string.Join(", ", dimensions), totalElements, tokenCount);

        // Handle different output shapes based on actual dimensions
        if (dimensions.Length == 3 && dimensions[2] >= 2)
        {
            // Shape: [batch, seq_len, num_classes] - class 1 indicates boundary
            var seqLen = Math.Min(tokenCount, dimensions[1]);
            for (var i = 0; i < seqLen; i++)
            {
                var boundaryScore = tensor[0, i, 1];
                var probability = Sigmoid(boundaryScore);
                if (probability >= _threshold)
                {
                    boundaries.Add(i);
                }
            }
        }
        else if (dimensions.Length == 2)
        {
            // Shape: [batch, seq_len] - direct scores
            var seqLen = Math.Min(tokenCount, dimensions[1]);
            for (var i = 0; i < seqLen; i++)
            {
                var probability = Sigmoid(tensor[0, i]);
                if (probability >= _threshold)
                {
                    boundaries.Add(i);
                }
            }
        }
        else if (dimensions.Length == 1)
        {
            // Shape: [seq_len] - flat array of scores
            var seqLen = Math.Min(tokenCount, dimensions[0]);
            for (var i = 0; i < seqLen; i++)
            {
                var probability = Sigmoid(tensor[i]);
                if (probability >= _threshold)
                {
                    boundaries.Add(i);
                }
            }
        }
    }

    private void ProcessOutputTensorFloat16(Tensor<Float16> tensor, int tokenCount, List<int> boundaries)
    {
        var dimensions = tensor.Dimensions.ToArray();
        var totalElements = (int)tensor.Length;

        _logger.LogDebug("Float16 tensor shape: [{Dims}], Total elements: {Total}, TokenCount: {Tokens}",
            string.Join(", ", dimensions), totalElements, tokenCount);

        // Handle different output shapes based on actual dimensions
        if (dimensions.Length == 3 && dimensions[2] >= 2)
        {
            // Shape: [batch, seq_len, num_classes]
            var seqLen = Math.Min(tokenCount, dimensions[1]);
            for (var i = 0; i < seqLen; i++)
            {
                var boundaryScore = (float)tensor[0, i, 1];
                var probability = Sigmoid(boundaryScore);
                if (probability >= _threshold)
                {
                    boundaries.Add(i);
                }
            }
        }
        else if (dimensions.Length == 2)
        {
            // Shape: [batch, seq_len]
            var seqLen = Math.Min(tokenCount, dimensions[1]);
            for (var i = 0; i < seqLen; i++)
            {
                var probability = Sigmoid((float)tensor[0, i]);
                if (probability >= _threshold)
                {
                    boundaries.Add(i);
                }
            }
        }
        else if (dimensions.Length == 1)
        {
            // Shape: [seq_len]
            var seqLen = Math.Min(tokenCount, dimensions[0]);
            for (var i = 0; i < seqLen; i++)
            {
                var probability = Sigmoid((float)tensor[i]);
                if (probability >= _threshold)
                {
                    boundaries.Add(i);
                }
            }
        }
    }

    private static List<string> SplitTextAtBoundaries(
        string text,
        IReadOnlyList<EncodedToken> tokens,
        List<int> boundaries)
    {
        if (boundaries.Count == 0 || string.IsNullOrEmpty(text))
        {
            return string.IsNullOrWhiteSpace(text) ? [] : [text.Trim()];
        }

        var result = new List<string>();
        var lastEnd = 0;

        foreach (var boundaryIndex in boundaries)
        {
            if (boundaryIndex >= tokens.Count)
            {
                continue;
            }

            var boundaryOffset = Math.Min(tokens[boundaryIndex].Offset.End.Value, text.Length);

            if (boundaryOffset > lastEnd && boundaryOffset <= text.Length)
            {
                var chunk = text.Substring(lastEnd, boundaryOffset - lastEnd).Trim();
                if (!string.IsNullOrWhiteSpace(chunk))
                {
                    result.Add(chunk);
                }
            }

            lastEnd = boundaryOffset;
        }

        // Add remaining text
        if (lastEnd < text.Length)
        {
            var remaining = text.Substring(lastEnd).Trim();
            if (!string.IsNullOrWhiteSpace(remaining))
            {
                result.Add(remaining);
            }
        }

        return result;
    }

    private List<string> MergeSmallParagraphs(List<string> paragraphs)
    {
        if (paragraphs.Count <= 1)
        {
            return paragraphs;
        }

        var result = new List<string>();
        var currentMerged = "";
        var maxChunkSize = _maxLength * 4;

        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrWhiteSpace(currentMerged))
            {
                currentMerged = paragraph;
            }
            else if (currentMerged.Length < _minChunkSize &&
                     currentMerged.Length + paragraph.Length + 2 <= maxChunkSize)
            {
                currentMerged += "\n\n" + paragraph;
            }
            else
            {
                result.Add(currentMerged);
                currentMerged = paragraph;
            }
        }

        if (!string.IsNullOrWhiteSpace(currentMerged))
        {
            result.Add(currentMerged);
        }

        return result;
    }

    private static float Sigmoid(float x) => 1.0f / (1.0f + MathF.Exp(-x));

    [GeneratedRegex(@"\n\s*\n", RegexOptions.Compiled)]
    private static partial Regex ParagraphSplitRegex();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _session.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
