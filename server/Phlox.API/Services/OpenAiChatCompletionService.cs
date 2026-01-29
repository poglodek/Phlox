using System.ClientModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using Phlox.API.Configuration;
using Phlox.API.Entities;

namespace Phlox.API.Services;

public class OpenAiChatCompletionService : IChatCompletionService
{
    private readonly ChatClient _client;
    private readonly ILogger<OpenAiChatCompletionService> _logger;

    private const string QueryRewriteSystemPrompt = """
        You are a search query optimizer. Your task is to rewrite the user's question into an optimized search query for a vector database.

        Rules:
        - Extract key concepts and terms from the question
        - Remove filler words and focus on semantic content
        - Keep the query concise but comprehensive
        - Output ONLY the rewritten query, nothing else
        """;

    private const string RagSystemPrompt = """
        You are a knowledgeable AI assistant. Your task is to answer the user's question using ONLY the information provided in the document context below.

        CRITICAL RULES:
        - You MUST answer the question using ONLY the information from the provided document context
        - NEVER say "I don't know", "I'm not sure", "The document doesn't contain", or similar phrases
        - If the exact answer is not in the context, use the available information to provide the most relevant and helpful response possible
        - Synthesize and connect information from different parts of the context to form a comprehensive answer
        - Be confident and direct in your responses
        - If you need to make reasonable inferences based on the context, do so without disclaimers
        - Cite or reference specific parts of the documents when it adds value
        - Maintain a helpful and informative tone throughout

        Remember: The user expects you to be knowledgeable about the provided documents. Always provide value from the context you have.
        """;

    public OpenAiChatCompletionService(
        IOptions<OpenAiOptions> options,
        ILogger<OpenAiChatCompletionService> logger)
    {
        _logger = logger;
        _client = new ChatClient(options.Value.ChatModel, options.Value.ApiKey);

        _logger.LogInformation("OpenAI Chat service initialized with model: {Model}", options.Value.ChatModel);
    }

    public async IAsyncEnumerable<string> StreamCompletionAsync(
        IEnumerable<MessageEntity> messages,
        string systemPrompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chatMessages = new List<ChatMessage>();

        // Add system prompt
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            chatMessages.Add(new SystemChatMessage(systemPrompt));
        }

        // Add conversation history
        foreach (var message in messages)
        {
            ChatMessage chatMessage = message.Role.ToLowerInvariant() switch
            {
                "user" => new UserChatMessage(message.Content),
                "assistant" => new AssistantChatMessage(message.Content),
                "system" => new SystemChatMessage(message.Content),
                _ => new UserChatMessage(message.Content)
            };

            chatMessages.Add(chatMessage);
        }

        _logger.LogDebug("Streaming chat completion with {MessageCount} messages", chatMessages.Count);

        AsyncCollectionResult<StreamingChatCompletionUpdate> streamingUpdates =
            _client.CompleteChatStreamingAsync(chatMessages, cancellationToken: cancellationToken);

        await foreach (var update in streamingUpdates.WithCancellation(cancellationToken))
        {
            foreach (var contentPart in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(contentPart.Text))
                {
                    yield return contentPart.Text;
                }
            }
        }
    }

    public async Task<string> RewriteQueryForSearchAsync(
        string userQuery,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Rewriting query for search: {Query}", userQuery);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(QueryRewriteSystemPrompt),
            new UserChatMessage(userQuery)
        };

        var response = await _client.CompleteChatAsync(messages, cancellationToken: cancellationToken);
        var rewrittenQuery = response.Value.Content[0].Text;

        _logger.LogDebug("Rewritten query: {RewrittenQuery}", rewrittenQuery);
        return rewrittenQuery;
    }

    public async IAsyncEnumerable<string> StreamAnswerWithContextAsync(
        string question,
        string documentContext,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Generating answer with context for question: {Question}", question);

        var userPrompt = $"""
            Document Context:
            {documentContext}

            Question: {question}
            """;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(RagSystemPrompt),
            new UserChatMessage(userPrompt)
        };

        AsyncCollectionResult<StreamingChatCompletionUpdate> streamingUpdates =
            _client.CompleteChatStreamingAsync(messages, cancellationToken: cancellationToken);

        await foreach (var update in streamingUpdates.WithCancellation(cancellationToken))
        {
            foreach (var contentPart in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(contentPart.Text))
                {
                    yield return contentPart.Text;
                }
            }
        }
    }
}
