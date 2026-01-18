namespace Phlox.API.Entities;

public class DocumentEntity
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public List<ParagraphsDocumentEntity> Paragraphs { get; set; } = [];
}