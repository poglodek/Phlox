using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Phlox.API.Services;

namespace Phlox.API.Tests.Services;

public class RagServiceTests
{
    private readonly Mock<IChatCompletionService> _chatCompletionServiceMock;
    private readonly Mock<IVectorService> _vectorServiceMock;
    private readonly RagService _sut;

    public RagServiceTests()
    {
        _chatCompletionServiceMock = new Mock<IChatCompletionService>();
        _vectorServiceMock = new Mock<IVectorService>();

        _sut = new RagService(
            _chatCompletionServiceMock.Object,
            _vectorServiceMock.Object,
            NullLogger<RagService>.Instance);
    }

    [Fact]
    public async Task GenerateAnswerAsync_RewritesQueryAndSearchesDocuments()
    {
        // Arrange
        var question = "What is the capital of France?";
        var rewrittenQuery = "capital France";
        var documentResults = new List<DocumentSearchResult>
        {
            new()
            {
                DocumentId = Guid.NewGuid(),
                Title = "Geography",
                Content = "Paris is the capital of France. It is known as the City of Light.",
                BestScore = 0.95f,
                RelevantParagraphs = ["Paris is the capital of France."]
            }
        };

        _chatCompletionServiceMock
            .Setup(x => x.RewriteQueryForSearchAsync(question, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rewrittenQuery);

        _vectorServiceMock
            .Setup(x => x.SearchDocumentsAsync(rewrittenQuery, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(documentResults);

        _chatCompletionServiceMock
            .Setup(x => x.StreamAnswerWithContextAsync(
                question,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable("Paris is the capital of France."));

        // Act
        var result = new List<string>();
        await foreach (var chunk in _sut.GenerateAnswerAsync(question))
        {
            result.Add(chunk);
        }

        // Assert
        _chatCompletionServiceMock.Verify(
            x => x.RewriteQueryForSearchAsync(question, It.IsAny<CancellationToken>()),
            Times.Once);

        _vectorServiceMock.Verify(
            x => x.SearchDocumentsAsync(rewrittenQuery, 3, It.IsAny<CancellationToken>()),
            Times.Once);

        _chatCompletionServiceMock.Verify(
            x => x.StreamAnswerWithContextAsync(
                question,
                It.Is<string>(ctx => ctx.Contains("Paris is the capital of France.")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        result.Should().Contain("Paris is the capital of France.");
    }

    [Fact]
    public async Task GenerateAnswerAsync_WhenNoDocumentsFound_ReturnsNotFoundMessage()
    {
        // Arrange
        var question = "What is the meaning of life?";
        var rewrittenQuery = "meaning life";

        _chatCompletionServiceMock
            .Setup(x => x.RewriteQueryForSearchAsync(question, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rewrittenQuery);

        _vectorServiceMock
            .Setup(x => x.SearchDocumentsAsync(rewrittenQuery, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentSearchResult>());

        // Act
        var result = new List<string>();
        await foreach (var chunk in _sut.GenerateAnswerAsync(question))
        {
            result.Add(chunk);
        }

        // Assert
        result.Should().Contain(s => s.Contains("no documents in the knowledge base"));

        _chatCompletionServiceMock.Verify(
            x => x.StreamAnswerWithContextAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GenerateAnswerAsync_SendsTop3DocumentsToLlm()
    {
        // Arrange
        var question = "Tell me about programming languages";
        var rewrittenQuery = "programming languages";
        var documentResults = new List<DocumentSearchResult>
        {
            new()
            {
                DocumentId = Guid.NewGuid(),
                Title = "C# Guide",
                Content = "C# is a modern, object-oriented programming language developed by Microsoft.",
                BestScore = 0.9f,
                RelevantParagraphs = ["C# is a modern programming language."]
            },
            new()
            {
                DocumentId = Guid.NewGuid(),
                Title = "Python Guide",
                Content = "Python is a versatile, high-level programming language known for readability.",
                BestScore = 0.85f,
                RelevantParagraphs = ["Python is a versatile programming language."]
            },
            new()
            {
                DocumentId = Guid.NewGuid(),
                Title = "JavaScript Guide",
                Content = "JavaScript is the programming language of the web.",
                BestScore = 0.8f,
                RelevantParagraphs = ["JavaScript is a web programming language."]
            }
        };

        _chatCompletionServiceMock
            .Setup(x => x.RewriteQueryForSearchAsync(question, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rewrittenQuery);

        _vectorServiceMock
            .Setup(x => x.SearchDocumentsAsync(rewrittenQuery, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(documentResults);

        _chatCompletionServiceMock
            .Setup(x => x.StreamAnswerWithContextAsync(
                question,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable("C#, Python, and JavaScript are popular languages."));

        // Act
        var result = new List<string>();
        await foreach (var chunk in _sut.GenerateAnswerAsync(question))
        {
            result.Add(chunk);
        }

        // Assert
        _chatCompletionServiceMock.Verify(
            x => x.StreamAnswerWithContextAsync(
                question,
                It.Is<string>(ctx =>
                    ctx.Contains("Document 1: C# Guide") &&
                    ctx.Contains("Document 2: Python Guide") &&
                    ctx.Contains("Document 3: JavaScript Guide") &&
                    ctx.Contains("C# is a modern, object-oriented programming language") &&
                    ctx.Contains("Python is a versatile, high-level programming language") &&
                    ctx.Contains("JavaScript is the programming language of the web")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateAnswerAsync_StreamsResponseChunks()
    {
        // Arrange
        var question = "What is AI?";
        var documentResults = new List<DocumentSearchResult>
        {
            new()
            {
                DocumentId = Guid.NewGuid(),
                Title = "AI Basics",
                Content = "AI stands for Artificial Intelligence. It is the simulation of human intelligence.",
                BestScore = 0.9f,
                RelevantParagraphs = ["AI stands for Artificial Intelligence."]
            }
        };

        _chatCompletionServiceMock
            .Setup(x => x.RewriteQueryForSearchAsync(question, It.IsAny<CancellationToken>()))
            .ReturnsAsync("AI artificial intelligence");

        _vectorServiceMock
            .Setup(x => x.SearchDocumentsAsync(It.IsAny<string>(), 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(documentResults);

        _chatCompletionServiceMock
            .Setup(x => x.StreamAnswerWithContextAsync(
                question,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable("AI ", "stands ", "for ", "Artificial ", "Intelligence."));

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in _sut.GenerateAnswerAsync(question))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Should().HaveCount(5);
        chunks.Should().ContainInOrder("AI ", "stands ", "for ", "Artificial ", "Intelligence.");
    }

    [Fact]
    public async Task GenerateAnswerAsync_IncludesFullDocumentContent()
    {
        // Arrange
        var question = "What are the features of C#?";
        var fullContent = """
            C# is a modern programming language.
            It supports object-oriented programming.
            It has strong typing and garbage collection.
            C# is widely used for enterprise applications.
            """;

        var documentResults = new List<DocumentSearchResult>
        {
            new()
            {
                DocumentId = Guid.NewGuid(),
                Title = "C# Features",
                Content = fullContent,
                BestScore = 0.95f,
                RelevantParagraphs = ["C# is a modern programming language."]
            }
        };

        _chatCompletionServiceMock
            .Setup(x => x.RewriteQueryForSearchAsync(question, It.IsAny<CancellationToken>()))
            .ReturnsAsync("C# features");

        _vectorServiceMock
            .Setup(x => x.SearchDocumentsAsync(It.IsAny<string>(), 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(documentResults);

        _chatCompletionServiceMock
            .Setup(x => x.StreamAnswerWithContextAsync(
                question,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable("C# has many features."));

        // Act
        await foreach (var _ in _sut.GenerateAnswerAsync(question)) { }

        // Assert
        _chatCompletionServiceMock.Verify(
            x => x.StreamAnswerWithContextAsync(
                question,
                It.Is<string>(ctx =>
                    ctx.Contains("object-oriented programming") &&
                    ctx.Contains("strong typing") &&
                    ctx.Contains("garbage collection") &&
                    ctx.Contains("enterprise applications")),
                It.IsAny<CancellationToken>()),
            Times.Once);
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
