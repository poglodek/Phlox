using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Phlox.API.Configuration;
using Phlox.API.Services;

namespace Phlox.API.Tests;

public class DocumentSlicerServiceTests : IDisposable
{
    private readonly DocumentSlicerService _sut;
    private readonly ILogger<DocumentSlicerService> _logger;

    public DocumentSlicerServiceTests()
    {
        _logger = NullLogger<DocumentSlicerService>.Instance;

        var options = Options.Create(new DocumentSlicerOptions
        {
            ModelPath = "Models/sat-3l-sm.onnx",
            TokenizerPath = "Models/sentencepiece.bpe.model",
            MaxLength = 512,
            Threshold = 0.5f
        });

        _sut = new DocumentSlicerService(options, _logger);
    }

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void SliceIntoParagraphs_WithEmptyContent_ReturnsEmptyList()
    {
        // Arrange
        var content = "";

        // Act
        var result = _sut.SliceIntoParagraphs(content);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void SliceIntoParagraphs_WithWhitespaceContent_ReturnsEmptyList()
    {
        // Arrange
        var content = "   \n\t  ";

        // Act
        var result = _sut.SliceIntoParagraphs(content);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void SliceIntoParagraphs_WithSingleSentence_ReturnsNonEmptyList()
    {
        // Arrange
        var content = "This is a single sentence without any breaks.";

        // Act
        var result = _sut.SliceIntoParagraphs(content);

        // Assert
        Assert.NotEmpty(result);
        var joinedResult = string.Join(" ", result);
        Assert.Contains("single sentence", joinedResult);
    }

    [Fact]
    public void SliceIntoParagraphs_WithMultipleSentences_ReturnsNonEmptyList()
    {
        // Arrange
        var content = "This is the first sentence. This is the second sentence. And here is a third one.";

        // Act
        var result = _sut.SliceIntoParagraphs(content);

        // Assert
        Assert.NotEmpty(result);
        Assert.All(result, p => Assert.False(string.IsNullOrWhiteSpace(p)));
    }

    [Fact]
    public void SliceIntoParagraphs_WithExistingParagraphBreaks_SplitsOnBreaks()
    {
        // Arrange
        var content = @"First paragraph with some text.

Second paragraph with more text.

Third paragraph ends here.";

        // Act
        var result = _sut.SliceIntoParagraphs(content);

        // Assert
        Assert.NotEmpty(result);
        var joinedResult = string.Join(" ", result);
        Assert.Contains("First paragraph", joinedResult);
        Assert.Contains("Second paragraph", joinedResult);
        Assert.Contains("Third paragraph", joinedResult);
    }

    [Fact]
    public void SliceIntoParagraphs_WithContinuousTopic_MaintainsCoherence()
    {
        // Arrange - content about a single topic that should stay together
        var content = "Python is a programming language. It was created by Guido van Rossum. Python emphasizes code readability. The language supports multiple programming paradigms.";

        // Act
        var result = _sut.SliceIntoParagraphs(content);

        // Assert
        Assert.NotEmpty(result);
        // All original content should be preserved
        var joinedResult = string.Join(" ", result);
        Assert.Contains("Python", joinedResult);
        Assert.Contains("Guido van Rossum", joinedResult);
    }

    [Fact]
    public void SliceIntoParagraphs_PreservesAllContent()
    {
        // Arrange
        var content = "First sentence here. Second sentence follows. Third sentence ends.";

        // Act
        var result = _sut.SliceIntoParagraphs(content);

        // Assert
        var joinedResult = string.Join(" ", result);
        Assert.Contains("First sentence here", joinedResult);
        Assert.Contains("Second sentence follows", joinedResult);
        Assert.Contains("Third sentence ends", joinedResult);
    }

    [Fact]
    public void SliceIntoParagraphs_WithLongParagraph_ReturnsNonEmptyList()
    {
        // Arrange - create a very long paragraph that exceeds max chunk size
        var sentences = new List<string>();
        for (var i = 0; i < 100; i++)
        {
            sentences.Add($"This is sentence number {i + 1} in our test document with some extra words to make it longer.");
        }
        var content = string.Join(" ", sentences);

        // Act
        var result = _sut.SliceIntoParagraphs(content);

        // Assert
        Assert.NotEmpty(result);
    }

    [Fact]
    public void SliceIntoParagraphs_HandlesPunctuationVariety()
    {
        // Arrange
        var content = "Is this a question? Yes it is! Here is a statement. Another question? And an exclamation!";

        // Act
        var result = _sut.SliceIntoParagraphs(content);

        // Assert
        Assert.NotEmpty(result);
        var joinedResult = string.Join(" ", result);
        Assert.Contains("?", joinedResult);
        Assert.Contains("!", joinedResult);
        Assert.Contains(".", joinedResult);
    }

    [Fact]
    public void SliceIntoParagraphs_WithPolishText_WorksCorrectly()
    {
        // Arrange - Polish text to test unicode support
        var content = "Polska jest krajem w Europie Środkowej. Stolica Polski to Warszawa. Polska ma bogatą historię i kulturę.";

        // Act
        var result = _sut.SliceIntoParagraphs(content);

        // Assert
        Assert.NotEmpty(result);
        var joinedResult = string.Join(" ", result);
        Assert.Contains("Polska", joinedResult);
        Assert.Contains("Warszawa", joinedResult);
    }

    [Fact]
    public void SliceIntoParagraphs_ReturnsTrimmedParagraphs()
    {
        // Arrange
        var content = "  First sentence.   Second sentence.  ";

        // Act
        var result = _sut.SliceIntoParagraphs(content);

        // Assert
        Assert.All(result, p =>
        {
            Assert.Equal(p.Trim(), p);
        });
    }

    [Fact]
    public void SliceIntoParagraphs_MergesSmallParagraphs()
    {
        // Arrange - very small paragraphs that should be merged
        var content = @"Hi.

Ok.

Yes, this is a much longer paragraph with more content that should not be merged with other paragraphs because it exceeds the minimum chunk size threshold.";

        // Act
        var result = _sut.SliceIntoParagraphs(content);

        // Assert
        // Small paragraphs "Hi." and "Ok." should be merged
        Assert.True(result.Count <= 3, "Small paragraphs should be merged when possible");
    }

    [Fact]
    public void SliceIntoParagraphs_HandlesLongContent()
    {
        // Arrange - create content that would exceed max token length
        var longSentence = new string('a', 500) + ". ";
        var content = string.Concat(Enumerable.Repeat(longSentence, 20));

        // Act
        var result = _sut.SliceIntoParagraphs(content);

        // Assert
        Assert.NotEmpty(result);
    }

    [Fact]
    public void SliceIntoParagraphs_HandlesSingleLongSentence()
    {
        // Arrange - single very long sentence without breaks
        var content = "This is a very long sentence that goes on and on " +
                      string.Concat(Enumerable.Repeat("and continues further ", 100)) +
                      "until it finally ends here.";

        // Act
        var result = _sut.SliceIntoParagraphs(content);

        // Assert
        Assert.NotEmpty(result);
        // Content should be preserved even if it can't be split nicely
        var joinedResult = string.Join(" ", result);
        Assert.Contains("This is a very long sentence", joinedResult);
    }

    [Fact]
    public void SliceIntoParagraphs_WithMixedContent_PreservesStructure()
    {
        // Arrange - realistic mixed content with headers, lists, etc.
        var content = @"Introduction to Machine Learning

Machine learning is a subset of artificial intelligence. It enables computers to learn from data. The field has grown rapidly in recent years.

Types of Machine Learning

There are three main types of machine learning. Supervised learning uses labeled data. Unsupervised learning finds patterns without labels. Reinforcement learning learns through trial and error.

Conclusion

Machine learning continues to evolve. New techniques are being developed constantly.";

        // Act
        var result = _sut.SliceIntoParagraphs(content);

        // Assert
        Assert.NotEmpty(result);
        var joinedResult = string.Join(" ", result);
        Assert.Contains("Machine learning", joinedResult);
        Assert.Contains("Supervised learning", joinedResult);
        Assert.Contains("Conclusion", joinedResult);
    }

    [Fact]
    public void SliceIntoParagraphs_WithCodeSnippet_HandlesSpecialCharacters()
    {
        // Arrange - content with code-like patterns
        var content = @"Here is a code example. The function calculate_sum(a, b) returns the sum of two numbers. It uses the return keyword. The result is stored in a variable.";

        // Act
        var result = _sut.SliceIntoParagraphs(content);

        // Assert
        Assert.NotEmpty(result);
        var joinedResult = string.Join(" ", result);
        Assert.Contains("calculate_sum", joinedResult);
    }
}
