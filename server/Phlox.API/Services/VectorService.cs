using Phlox.API.Entities;

namespace Phlox.API.Services;

public class VectorService : IVectorService
{
    public Task AddDocument(DocumentEntity document, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<List<DocumentEntity>> Search(string phrase, int documents = 3, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}