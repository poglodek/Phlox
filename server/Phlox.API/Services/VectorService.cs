using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Phlox.API.Configuration;
using Phlox.API.Data;
using Phlox.API.Entities;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Phlox.API.Services;

public class VectorService : IVectorService
{
    private readonly QdrantClient _qdrantClient;
    private readonly IDocumentSlicerService _documentSlicer;
    private readonly IEmbeddingService _embeddingService;
    private readonly IHtmlContentCleanerService _htmlCleaner;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<VectorService> _logger;
    private readonly QdrantOptions _qdrantOptions;

    public VectorService(
        IOptions<QdrantOptions> qdrantOptions,
        IDocumentSlicerService documentSlicer,
        IEmbeddingService embeddingService,
        IHtmlContentCleanerService htmlCleaner,
        ApplicationDbContext dbContext,
        ILogger<VectorService> logger)
    {
        _qdrantOptions = qdrantOptions.Value;
        _documentSlicer = documentSlicer;
        _embeddingService = embeddingService;
        _htmlCleaner = htmlCleaner;
        _dbContext = dbContext;
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

        // Clean HTML content and extract images
        var cleaningResult = await _htmlCleaner.CleanHtmlAsync(document.Content, cancellationToken);

        _logger.LogInformation(
            "HTML cleaning complete: {TextLength} chars of text, {ImageCount} images found",
            cleaningResult.CleanedText.Length,
            cleaningResult.ImageDescriptions.Count);

        // Slice the cleaned text content into paragraphs using sat-3l-sm model
        var paragraphTexts = _documentSlicer.SliceIntoParagraphs(cleaningResult.CleanedText);

        // Add image descriptions as additional paragraphs
        foreach (var imageDesc in cleaningResult.ImageDescriptions)
        {
            if (!string.IsNullOrWhiteSpace(imageDesc.Description))
            {
                paragraphTexts.Add($"[Image content: {imageDesc.Description}]");
            }
        }

        _logger.LogInformation("Document processed into {ParagraphCount} paragraphs (including image descriptions)", paragraphTexts.Count);

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

    public async Task<List<DocumentSearchResult>> SearchDocumentsAsync(
        string phrase,
        int documentLimit = 3,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching for top {Limit} documents matching: {Phrase}", documentLimit, phrase);

        await EnsureCollectionExistsAsync(cancellationToken);

        // Generate embedding for the search phrase
        var queryEmbedding = _embeddingService.GenerateEmbedding(phrase);

        // Search for more results to ensure we get enough unique documents
        var searchLimit = documentLimit * 5;
        var searchResults = await _qdrantClient.SearchAsync(
            _qdrantOptions.CollectionName,
            queryEmbedding,
            limit: (ulong)searchLimit,
            cancellationToken: cancellationToken);

        // Group by document and get the best score for each
        var documentGroups = searchResults
            .Where(r => Guid.TryParse(r.Payload["document_id"].StringValue, out _))
            .GroupBy(r => Guid.Parse(r.Payload["document_id"].StringValue))
            .Select(g => new
            {
                DocumentId = g.Key,
                BestScore = g.Max(r => r.Score),
                Title = g.First().Payload["title"].StringValue,
                RelevantParagraphs = g
                    .OrderByDescending(r => r.Score)
                    .Take(3)
                    .Select(r => r.Payload["paragraph_content"].StringValue)
                    .ToList()
            })
            .OrderByDescending(d => d.BestScore)
            .Take(documentLimit)
            .ToList();

        if (documentGroups.Count == 0)
        {
            _logger.LogInformation("No documents found for query");
            return [];
        }

        // Fetch full document content from database
        var documentIds = documentGroups.Select(d => d.DocumentId).ToList();
        var documents = await _dbContext.Documents
            .Where(d => documentIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Content, cancellationToken);

        var results = documentGroups
            .Select(g => new DocumentSearchResult
            {
                DocumentId = g.DocumentId,
                Title = g.Title,
                Content = documents.GetValueOrDefault(g.DocumentId, string.Join("\n\n", g.RelevantParagraphs)),
                BestScore = g.BestScore,
                RelevantParagraphs = g.RelevantParagraphs
            })
            .ToList();

        _logger.LogInformation("Search returned {ResultCount} unique documents", results.Count);
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
