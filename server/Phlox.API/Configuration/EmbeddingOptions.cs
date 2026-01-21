namespace Phlox.API.Configuration;

public class EmbeddingOptions
{
    public const string SectionName = "Embedding";

    public required string ModelPath { get; set; }
    public required string TokenizerPath { get; set; }
    public int MaxTokens { get; set; } = 512;
}
