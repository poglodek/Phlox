using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using Phlox.API.Configuration;

namespace Phlox.API.Services;

public class EmbeddingService : IEmbeddingService, IDisposable
{
    private readonly InferenceSession _session;
    private readonly SentencePieceTokenizer _tokenizer;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly int _maxTokens;
    private bool _disposed;

    public EmbeddingService(IOptions<EmbeddingOptions> options, ILogger<EmbeddingService> logger)
    {
        _logger = logger;
        _maxTokens = options.Value.MaxTokens;

        var modelPath = options.Value.ModelPath;
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"Embedding model not found at path: {modelPath}");
        }

        var tokenizerPath = options.Value.TokenizerPath;
        if (!File.Exists(tokenizerPath))
        {
            throw new FileNotFoundException($"Tokenizer model not found at path: {tokenizerPath}");
        }

        _session = new InferenceSession(modelPath);

        // Load SentencePiece tokenizer
        using var tokenizerStream = File.OpenRead(tokenizerPath);
        _tokenizer = SentencePieceTokenizer.Create(tokenizerStream, true, false, null);

        _logger.LogInformation("Embedding service initialized with model: {ModelPath}", modelPath);
    }

    public float[] GenerateEmbedding(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Empty text provided for embedding generation");
            return [];
        }

        var (inputIds, attentionMask) = Tokenize(text);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask)
        };

        using var results = _session.Run(inputs);

        // Get the last hidden state and perform mean pooling
        var lastHiddenState = results.First().AsTensor<float>();
        var embedding = MeanPooling(lastHiddenState, attentionMask);

        return embedding;
    }

    public List<float[]> GenerateEmbeddings(IEnumerable<string> texts)
    {
        return texts.Select(GenerateEmbedding).ToList();
    }

    private (DenseTensor<long> inputIds, DenseTensor<long> attentionMask) Tokenize(string text)
    {
        var encoding = _tokenizer.EncodeToIds(text, _maxTokens, out _, out _);
        var tokenCount = encoding.Count;

        // Create tensors with batch size 1
        var inputIds = new DenseTensor<long>(new[] { 1, tokenCount });
        var attentionMask = new DenseTensor<long>(new[] { 1, tokenCount });

        for (var i = 0; i < tokenCount; i++)
        {
            inputIds[0, i] = encoding[i];
            attentionMask[0, i] = 1;
        }

        return (inputIds, attentionMask);
    }

    private static float[] MeanPooling(Tensor<float> lastHiddenState, DenseTensor<long> attentionMask)
    {
        var dimensions = lastHiddenState.Dimensions;
        var sequenceLength = (int)dimensions[1];
        var hiddenSize = (int)dimensions[2];

        var embedding = new float[hiddenSize];
        var validTokenCount = 0L;

        for (var i = 0; i < sequenceLength; i++)
        {
            var mask = attentionMask[0, i];
            if (mask > 0)
            {
                validTokenCount += mask;
                for (var j = 0; j < hiddenSize; j++)
                {
                    embedding[j] += lastHiddenState[0, i, j] * mask;
                }
            }
        }

        // Normalize by the number of valid tokens
        if (validTokenCount > 0)
        {
            for (var j = 0; j < hiddenSize; j++)
            {
                embedding[j] /= validTokenCount;
            }
        }

        // L2 normalize the embedding
        var norm = (float)Math.Sqrt(embedding.Sum(x => x * x));
        if (norm > 0)
        {
            for (var j = 0; j < hiddenSize; j++)
            {
                embedding[j] /= norm;
            }
        }

        return embedding;
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
