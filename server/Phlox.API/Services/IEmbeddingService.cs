namespace Phlox.API.Services;

public interface IEmbeddingService
{
    float[] GenerateEmbedding(string text);
    List<float[]> GenerateEmbeddings(IEnumerable<string> texts);
}
