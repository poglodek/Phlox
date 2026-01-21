namespace Phlox.API.DTOs;

public class DocumentResponse
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public required string Content { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<ParagraphResponse> Paragraphs { get; set; } = [];
}

public class ParagraphResponse
{
    public Guid Id { get; set; }
    public int Index { get; set; }
    public required string Content { get; set; }
}
