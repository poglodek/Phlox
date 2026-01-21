namespace Phlox.API.Configuration;

public class DocumentSlicerOptions
{
    public const string SectionName = "DocumentSlicer";

    public required string ModelPath { get; set; }
    public required string TokenizerPath { get; set; }
    public int MaxLength { get; set; } = 512;
    public float Threshold { get; set; } = 0.5f;
}
