namespace Phlox.API.Services;

public interface IRagService
{
    IAsyncEnumerable<string> GenerateAnswerAsync(
        string question,
        CancellationToken cancellationToken = default);
}
