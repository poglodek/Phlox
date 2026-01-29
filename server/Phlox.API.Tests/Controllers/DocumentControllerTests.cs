using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Phlox.API.Controllers;
using Phlox.API.Data;
using Phlox.API.DTOs;
using Phlox.API.Entities;
using Phlox.API.Services;
using Phlox.API.Tests.Fixtures;

namespace Phlox.API.Tests.Controllers;

public class DocumentControllerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly Mock<IVectorService> _vectorServiceMock;
    private readonly DocumentController _sut;

    public DocumentControllerTests()
    {
        _dbContext = TestDbContextFactory.Create();
        _vectorServiceMock = new Mock<IVectorService>();

        _sut = new DocumentController(
            _dbContext,
            _vectorServiceMock.Object,
            NullLogger<DocumentController>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    #region CreateDocument Tests

    [Fact]
    public async Task CreateDocument_WithValidRequest_ReturnsCreatedWithDocument()
    {
        // Arrange
        var request = new AddDocumentRequest
        {
            Title = "Test Document",
            Content = "This is the document content."
        };

        var paragraphs = new List<ParagraphEntity>
        {
            TestDataFactory.CreateParagraph(index: 0, content: "This is the document content.")
        };

        _vectorServiceMock.Setup(x => x.AddDocumentAsync(It.IsAny<DocumentEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paragraphs);

        // Act
        var result = await _sut.CreateDocument(request, CancellationToken.None);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = createdResult.Value.Should().BeOfType<DocumentResponse>().Subject;
        response.Title.Should().Be("Test Document");
        response.Content.Should().Be("This is the document content.");
        response.Paragraphs.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateDocument_SavesDocumentToDatabase()
    {
        // Arrange
        var request = new AddDocumentRequest
        {
            Title = "Test Document",
            Content = "Content"
        };

        _vectorServiceMock.Setup(x => x.AddDocumentAsync(It.IsAny<DocumentEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ParagraphEntity>());

        // Act
        await _sut.CreateDocument(request, CancellationToken.None);

        // Assert
        _dbContext.Documents.Should().HaveCount(1);
        var savedDocument = _dbContext.Documents.Single();
        savedDocument.Title.Should().Be("Test Document");
    }

    [Fact]
    public async Task CreateDocument_CallsVectorService()
    {
        // Arrange
        var request = new AddDocumentRequest
        {
            Title = "Test Document",
            Content = "Content"
        };

        _vectorServiceMock.Setup(x => x.AddDocumentAsync(It.IsAny<DocumentEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ParagraphEntity>());

        // Act
        await _sut.CreateDocument(request, CancellationToken.None);

        // Assert
        _vectorServiceMock.Verify(x => x.AddDocumentAsync(
            It.Is<DocumentEntity>(d => d.Title == "Test Document"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetDocument Tests

    [Fact]
    public async Task GetDocument_WhenDocumentExists_ReturnsOkWithDocument()
    {
        // Arrange
        var document = TestDataFactory.CreateDocument(title: "My Document", content: "My content");
        document.Paragraphs.Add(TestDataFactory.CreateParagraph(documentId: document.Id, index: 0));
        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetDocument(document.Id, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<DocumentResponse>().Subject;
        response.Id.Should().Be(document.Id);
        response.Title.Should().Be("My Document");
        response.Paragraphs.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetDocument_WhenDocumentNotFound_ReturnsNotFound()
    {
        // Arrange - no documents in database

        // Act
        var result = await _sut.GetDocument(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetDocument_ReturnsParagraphsOrderedByIndex()
    {
        // Arrange
        var document = TestDataFactory.CreateDocument();
        document.Paragraphs.Add(TestDataFactory.CreateParagraph(documentId: document.Id, index: 2, content: "Third"));
        document.Paragraphs.Add(TestDataFactory.CreateParagraph(documentId: document.Id, index: 0, content: "First"));
        document.Paragraphs.Add(TestDataFactory.CreateParagraph(documentId: document.Id, index: 1, content: "Second"));
        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetDocument(document.Id, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<DocumentResponse>().Subject;
        response.Paragraphs.Should().HaveCount(3);
        response.Paragraphs[0].Content.Should().Be("First");
        response.Paragraphs[1].Content.Should().Be("Second");
        response.Paragraphs[2].Content.Should().Be("Third");
    }

    #endregion

    #region GetDocuments Tests

    [Fact]
    public async Task GetDocuments_ReturnsAllDocuments()
    {
        // Arrange
        _dbContext.Documents.AddRange(
            TestDataFactory.CreateDocument(title: "Doc 1"),
            TestDataFactory.CreateDocument(title: "Doc 2"),
            TestDataFactory.CreateDocument(title: "Doc 3"));
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetDocuments(1, 10, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<List<DocumentResponse>>().Subject;
        response.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetDocuments_RespectsPagination()
    {
        // Arrange
        for (var i = 0; i < 25; i++)
        {
            _dbContext.Documents.Add(TestDataFactory.CreateDocument(title: $"Doc {i}"));
        }
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetDocuments(1, 10, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<List<DocumentResponse>>().Subject;
        response.Should().HaveCount(10);
    }

    [Fact]
    public async Task GetDocuments_ReturnsSecondPage()
    {
        // Arrange
        for (var i = 0; i < 25; i++)
        {
            _dbContext.Documents.Add(TestDataFactory.CreateDocument(title: $"Doc {i}"));
        }
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetDocuments(2, 10, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<List<DocumentResponse>>().Subject;
        response.Should().HaveCount(10);
    }

    [Fact]
    public async Task GetDocuments_WhenEmpty_ReturnsEmptyList()
    {
        // Act
        var result = await _sut.GetDocuments(1, 10, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<List<DocumentResponse>>().Subject;
        response.Should().BeEmpty();
    }

    #endregion

    #region DeleteDocument Tests

    [Fact]
    public async Task DeleteDocument_WhenDocumentExists_ReturnsNoContent()
    {
        // Arrange
        var document = TestDataFactory.CreateDocument();
        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.DeleteDocument(document.Id, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _dbContext.Documents.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteDocument_WhenDocumentNotFound_ReturnsNotFound()
    {
        // Act
        var result = await _sut.DeleteDocument(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteDocument_DeletesFromVectorStore()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var document = TestDataFactory.CreateDocument(id: documentId);
        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.DeleteDocument(documentId, CancellationToken.None);

        // Assert
        _vectorServiceMock.Verify(x => x.DeleteDocumentAsync(documentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteDocument_DoesNotCallVectorServiceWhenNotFound()
    {
        // Act
        await _sut.DeleteDocument(Guid.NewGuid(), CancellationToken.None);

        // Assert
        _vectorServiceMock.Verify(x => x.DeleteDocumentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
