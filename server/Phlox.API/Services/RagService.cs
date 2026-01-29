using System.Runtime.CompilerServices;
using System.Text;

namespace Phlox.API.Services;

public class RagService : IRagService
{
    private readonly IChatCompletionService _chatCompletionService;
    private readonly IVectorService _vectorService;
    private readonly ILogger<RagService> _logger;

    private const int MaxSearchResults = 3;

    public RagService(
        IChatCompletionService chatCompletionService,
        IVectorService vectorService,
        ILogger<RagService> logger)
    {
        _chatCompletionService = chatCompletionService;
        _vectorService = vectorService;
        _logger = logger;
    }

    public async IAsyncEnumerable<string> GenerateAnswerAsync(
        string question,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing RAG query: {Question}", question);

        // Step 1: Rewrite the query for better search results
        var searchQuery = await _chatCompletionService.RewriteQueryForSearchAsync(question, cancellationToken);
        _logger.LogDebug("Search query: {SearchQuery}", searchQuery);

        // Step 2: Search Qdrant for top 3 related documents
        var documents = await _vectorService.SearchDocumentsAsync(searchQuery, MaxSearchResults, cancellationToken);

        if (documents.Count == 0 || documents.FirstOrDefault()!.BestScore < 0.35)
        {
            _logger.LogWarning("No relevant documents found for query: {Query}", searchQuery);
            yield return "There are no documents in the knowledge base that match your question. Please upload relevant documents first or try rephrasing your question.";
            yield break;
        }

        _logger.LogDebug("Found {Count} relevant document(s)", documents.Count);

        // Step 3: Build context from full documents
        var contextBuilder = new StringBuilder();
        for (var i = 0; i < documents.Count; i++)
        {
            var doc = documents[i];
            contextBuilder.AppendLine($"=== Document {i + 1}: {doc.Title} ===");
            contextBuilder.AppendLine(doc.Content);
            contextBuilder.AppendLine();
        }

        var documentContext = contextBuilder.ToString();
        _logger.LogDebug("Document context size: {Size} characters", documentContext.Length);

        // Step 4: Generate answer using LLM with full document context
        await foreach (var chunk in _chatCompletionService.StreamAnswerWithContextAsync(
            question,
            documentContext,
            cancellationToken))
        {
            yield return chunk;
        }

        _logger.LogInformation("RAG response completed for query: {Question}", question);
    }
}
