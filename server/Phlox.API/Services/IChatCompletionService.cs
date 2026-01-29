using Phlox.API.Entities;

namespace Phlox.API.Services;

public interface IChatCompletionService
{
    IAsyncEnumerable<string> StreamCompletionAsync(
        IEnumerable<MessageEntity> messages,
        string systemPrompt,
        CancellationToken cancellationToken = default);

    Task<string> RewriteQueryForSearchAsync(
        string userQuery,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamAnswerWithContextAsync(
        string question,
        string documentContext,
        CancellationToken cancellationToken = default);
}
