using Microsoft.Extensions.Options;
using OpenAI.Embeddings;
using Phlox.API.Configuration;

namespace Phlox.API.Services;

public class OpenAiEmbeddingService : IEmbeddingService
{
    private readonly EmbeddingClient _client;
    private readonly ILogger<OpenAiEmbeddingService> _logger;
    private readonly int _dimensions;

    public OpenAiEmbeddingService(IOptions<OpenAiOptions> options, ILogger<OpenAiEmbeddingService> logger)
    {
        _logger = logger;
        _dimensions = options.Value.Dimensions;

        _client = new EmbeddingClient(options.Value.Model, options.Value.ApiKey);

        _logger.LogInformation("OpenAI Embedding service initialized with model: {Model}", options.Value.Model);
    }

    public float[] GenerateEmbedding(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Empty text provided for embedding generation");
            return [];
        }

        var options = new EmbeddingGenerationOptions
        {
            Dimensions = _dimensions
        };

        var response = _client.GenerateEmbedding(text, options);
        var embedding = response.Value.ToFloats();

        return embedding.ToArray();
    }

    public List<float[]> GenerateEmbeddings(IEnumerable<string> texts)
    {
        var textList = texts.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();

        if (textList.Count == 0)
        {
            _logger.LogWarning("No valid texts provided for embedding generation");
            return [];
        }

        var options = new EmbeddingGenerationOptions
        {
            Dimensions = _dimensions
        };

        var response = _client.GenerateEmbeddings(textList, options);

        return response.Value
            .Select(e => e.ToFloats().ToArray())
            .ToList();
    }
}
