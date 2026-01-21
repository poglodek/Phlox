namespace Phlox.API.Configuration;

public class QdrantOptions
{
    public const string SectionName = "Qdrant";

    public required string Host { get; set; }
    public int Port { get; set; } = 6334;
    public string CollectionName { get; set; } = "documents";
    public int VectorSize { get; set; } = 768;
    public string? ApiKey { get; set; }
    public bool UseTls { get; set; } = false;
}
