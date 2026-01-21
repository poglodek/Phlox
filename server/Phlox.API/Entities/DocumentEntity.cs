namespace Phlox.API.Entities;

public class DocumentEntity
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public required string Content { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public List<ParagraphEntity> Paragraphs { get; set; } = [];
}
