namespace Phlox.API.Configuration;

public class OpenAiOptions
{
    public const string SectionName = "OpenAi";

    public required string ApiKey { get; set; }
    public string Model { get; set; } = "text-embedding-3-small";
    public string ChatModel { get; set; } = "gpt-4o-mini";
    public int Dimensions { get; set; } = 1536;
}
