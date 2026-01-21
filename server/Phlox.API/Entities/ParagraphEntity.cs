namespace Phlox.API.Entities;

public class ParagraphEntity
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int Index { get; set; }
    public required string Content { get; set; }

    public DocumentEntity Document { get; set; } = null!;
}
