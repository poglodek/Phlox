using Microsoft.Extensions.Options;
using Phlox.API.Configuration;
using Phlox.API.Entities;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Phlox.API.Services;

public class VectorService : IVectorService
{
    private readonly QdrantClient _qdrantClient;
    private readonly IDocumentSlicerService _documentSlicer;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<VectorService> _logger;
    private readonly QdrantOptions _qdrantOptions;

    public VectorService(
        IOptions<QdrantOptions> qdrantOptions,
        IDocumentSlicerService documentSlicer,
        IEmbeddingService embeddingService,
        ILogger<VectorService> logger)
    {
        _qdrantOptions = qdrantOptions.Value;
        _documentSlicer = documentSlicer;
        _embeddingService = embeddingService;
        _logger = logger;

        _qdrantClient = new QdrantClient(
            host: _qdrantOptions.Host,
            port: _qdrantOptions.Port,
            https: _qdrantOptions.UseTls,
            apiKey: _qdrantOptions.ApiKey);
    }

    public async Task<List<ParagraphEntity>> AddDocumentAsync(DocumentEntity document, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adding document {DocumentId} with title: {Title}", document.Id, document.Title);

        await EnsureCollectionExistsAsync(cancellationToken);

        // Slice the document content into paragraphs using sat-3l-sm model
        var paragraphTexts = _documentSlicer.SliceIntoParagraphs(document.Content);

        _logger.LogInformation("Document sliced into {ParagraphCount} paragraphs", paragraphTexts.Count);

        var points = new List<PointStruct>();
        var paragraphEntities = new List<ParagraphEntity>();

        for (var i = 0; i < paragraphTexts.Count; i++)
        {
            var paragraphText = paragraphTexts[i];

            // Create paragraph entity
            var paragraphEntity = new ParagraphEntity
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                Index = i,
                Content = paragraphText
            };
            paragraphEntities.Add(paragraphEntity);

            // Generate embedding for the paragraph
            var embedding = _embeddingService.GenerateEmbedding(paragraphText);

            var point = new PointStruct
            {
                Id = paragraphEntity.Id,
                Vectors = embedding,
                Payload =
                {
                    ["document_id"] = document.Id.ToString(),
                    ["paragraph_id"] = paragraphEntity.Id.ToString(),
                    ["title"] = document.Title,
                    ["paragraph_index"] = i,
                    ["paragraph_content"] = paragraphText
                }
            };

            points.Add(point);
        }

        // Upsert points in batches
        const int batchSize = 100;
        for (var i = 0; i < points.Count; i += batchSize)
        {
            var batch = points.Skip(i).Take(batchSize).ToList();
            await _qdrantClient.UpsertAsync(_qdrantOptions.CollectionName, batch, cancellationToken: cancellationToken);
            _logger.LogDebug("Upserted batch {BatchIndex} with {PointCount} points", i / batchSize, batch.Count);
        }

        _logger.LogInformation("Successfully added document {DocumentId} with {ParagraphCount} paragraphs to vector store",
            document.Id, paragraphTexts.Count);

        return paragraphEntities;
    }

    public async Task<List<SearchResult>> SearchAsync(string phrase, int limit = 3, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching for: {Phrase}", phrase);

        await EnsureCollectionExistsAsync(cancellationToken);

        // Generate embedding for the search phrase
        var queryEmbedding = _embeddingService.GenerateEmbedding(phrase);

        // Search in Qdrant
        var searchResults = await _qdrantClient.SearchAsync(
            _qdrantOptions.CollectionName,
            queryEmbedding,
            limit: (ulong)limit,
            cancellationToken: cancellationToken);

        var results = new List<SearchResult>();

        foreach (var result in searchResults)
        {
            var documentIdStr = result.Payload["document_id"].StringValue;
            if (!Guid.TryParse(documentIdStr, out var documentId))
                continue;

            results.Add(new SearchResult
            {
                DocumentId = documentId,
                Title = result.Payload["title"].StringValue,
                ParagraphContent = result.Payload["paragraph_content"].StringValue,
                ParagraphIndex = (int)result.Payload["paragraph_index"].IntegerValue,
                Score = result.Score
            });
        }

        _logger.LogInformation("Search returned {ResultCount} results", results.Count);
        return results;
    }

    public async Task DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting document {DocumentId} from vector store", documentId);

        await EnsureCollectionExistsAsync(cancellationToken);

        // Delete all points with this document_id
        await _qdrantClient.DeleteAsync(
            _qdrantOptions.CollectionName,
            new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "document_id",
                            Match = new Match { Keyword = documentId.ToString() }
                        }
                    }
                }
            },
            cancellationToken: cancellationToken);

        _logger.LogInformation("Successfully deleted document {DocumentId} from vector store", documentId);
    }

    private async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken)
    {
        var collections = await _qdrantClient.ListCollectionsAsync(cancellationToken);

        if (collections.All(c => c != _qdrantOptions.CollectionName))
        {
            _logger.LogInformation("Creating collection: {CollectionName}", _qdrantOptions.CollectionName);

            await _qdrantClient.CreateCollectionAsync(
                _qdrantOptions.CollectionName,
                new VectorParams
                {
                    Size = (ulong)_qdrantOptions.VectorSize,
                    Distance = Distance.Cosine
                },
                cancellationToken: cancellationToken);

            _logger.LogInformation("Collection {CollectionName} created successfully", _qdrantOptions.CollectionName);
        }
    }
}
