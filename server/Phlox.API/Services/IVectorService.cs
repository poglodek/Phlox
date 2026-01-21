using Phlox.API.Entities;

namespace Phlox.API.Services;

public interface IVectorService
{
    Task<List<ParagraphEntity>> AddDocumentAsync(DocumentEntity document, CancellationToken cancellationToken = default);
    Task<List<SearchResult>> SearchAsync(string phrase, int limit = 3, CancellationToken cancellationToken = default);
    Task DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
}

public class SearchResult
{
    public Guid DocumentId { get; set; }
    public required string Title { get; set; }
    public required string ParagraphContent { get; set; }
    public int ParagraphIndex { get; set; }
    public float Score { get; set; }
}
