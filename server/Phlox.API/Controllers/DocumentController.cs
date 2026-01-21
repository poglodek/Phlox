using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Phlox.API.Data;
using Phlox.API.DTOs;
using Phlox.API.Entities;
using Phlox.API.Services;

namespace Phlox.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class DocumentController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IVectorService _vectorService;
    private readonly ILogger<DocumentController> _logger;

    public DocumentController(
        ApplicationDbContext dbContext,
        IVectorService vectorService,
        ILogger<DocumentController> logger)
    {
        _dbContext = dbContext;
        _vectorService = vectorService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<DocumentResponse>> CreateDocument(
        [FromBody] AddDocumentRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating document with title: {Title}", request.Title);

        // Create document entity
        var document = new DocumentEntity
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Content = request.Content,
            CreatedAt = DateTime.UtcNow
        };

        // Add to vector store and get paragraphs
        var paragraphs = await _vectorService.AddDocumentAsync(document, cancellationToken);

        // Set paragraphs on document
        document.Paragraphs = paragraphs;

        // Save to SQL database
        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Document {DocumentId} created successfully", document.Id);

        return CreatedAtAction(
            nameof(GetDocument),
            new { id = document.Id },
            MapToResponse(document));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentResponse>> GetDocument(
        Guid id,
        CancellationToken cancellationToken)
    {
        var document = await _dbContext.Documents
            .Include(d => d.Paragraphs.OrderBy(p => p.Index))
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(document));
    }

    [HttpGet]
    public async Task<ActionResult<List<DocumentResponse>>> GetDocuments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var documents = await _dbContext.Documents
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(d => d.Paragraphs.OrderBy(p => p.Index))
            .ToListAsync(cancellationToken);

        return Ok(documents.Select(MapToResponse).ToList());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteDocument(
        Guid id,
        CancellationToken cancellationToken)
    {
        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        // Delete from vector store
        await _vectorService.DeleteDocumentAsync(id, cancellationToken);

        // Delete from SQL database (cascade deletes paragraphs)
        _dbContext.Documents.Remove(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Document {DocumentId} deleted successfully", id);

        return NoContent();
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<SearchResult>>> SearchDocuments(
        [FromQuery] string query,
        [FromQuery] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Query cannot be empty");
        }

        var results = await _vectorService.SearchAsync(query, limit, cancellationToken);
        return Ok(results);
    }

    private static DocumentResponse MapToResponse(DocumentEntity document)
    {
        return new DocumentResponse
        {
            Id = document.Id,
            Title = document.Title,
            Content = document.Content,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt,
            Paragraphs = document.Paragraphs.Select(p => new ParagraphResponse
            {
                Id = p.Id,
                Index = p.Index,
                Content = p.Content
            }).ToList()
        };
    }
}
