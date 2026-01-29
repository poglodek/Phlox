using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Phlox.API.Configuration;
using Phlox.API.Services;

namespace Phlox.API.Tests.Services;

public class ChatCompletionServiceTests
{
    [Fact]
    public void IChatCompletionService_HasRewriteQueryMethod()
    {
        // This test verifies the interface contract
        var mockService = new Mock<IChatCompletionService>();

        mockService
            .Setup(x => x.RewriteQueryForSearchAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("rewritten query");

        // Act & Assert - should not throw
        var result = mockService.Object.RewriteQueryForSearchAsync("test query", CancellationToken.None);
        result.Should().NotBeNull();
    }

    [Fact]
    public void IChatCompletionService_HasStreamAnswerWithContextMethod()
    {
        // This test verifies the interface contract
        var mockService = new Mock<IChatCompletionService>();

        mockService
            .Setup(x => x.StreamAnswerWithContextAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable("test"));

        // Act & Assert - should not throw
        var result = mockService.Object.StreamAnswerWithContextAsync(
            "question",
            "context",
            CancellationToken.None);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task RewriteQueryForSearchAsync_MockReturnsOptimizedQuery()
    {
        // Arrange
        var mockService = new Mock<IChatCompletionService>();
        var originalQuery = "What is the best way to learn programming?";
        var expectedRewritten = "learn programming best practices";

        mockService
            .Setup(x => x.RewriteQueryForSearchAsync(originalQuery, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRewritten);

        // Act
        var result = await mockService.Object.RewriteQueryForSearchAsync(originalQuery, CancellationToken.None);

        // Assert
        result.Should().Be(expectedRewritten);
        mockService.Verify(
            x => x.RewriteQueryForSearchAsync(originalQuery, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StreamAnswerWithContextAsync_MockStreamsChunks()
    {
        // Arrange
        var mockService = new Mock<IChatCompletionService>();
        var question = "What is AI?";
        var context = "AI stands for Artificial Intelligence.";
        var expectedChunks = new[] { "AI ", "is ", "Artificial ", "Intelligence." };

        mockService
            .Setup(x => x.StreamAnswerWithContextAsync(question, context, It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable(expectedChunks));

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in mockService.Object.StreamAnswerWithContextAsync(question, context, CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Should().BeEquivalentTo(expectedChunks);
    }

    private static async IAsyncEnumerable<string> AsyncEnumerable(params string[] items)
    {
        foreach (var item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }
}
