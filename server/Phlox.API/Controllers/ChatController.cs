using System.Text;
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
[Authorize]
public class ChatController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IRagService _ragService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        ApplicationDbContext dbContext,
        IRagService ragService,
        ICurrentUserService currentUserService,
        ILogger<ChatController> logger)
    {
        _dbContext = dbContext;
        _ragService = ragService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> CreateChat(
        [FromBody] CreateChatRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
        {
            return Unauthorized();
        }

        var chat = new ChatEntity
        {
            Id = Guid.NewGuid(),
            OwnerId = userId.Value,
            Title = request?.Title ?? "New Chat",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Chats.Add(chat);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Chat {ChatId} created for user {UserId}", chat.Id, userId);

        return CreatedAtAction(
            nameof(GetChat),
            new { id = chat.Id },
            MapToResponse(chat));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ChatResponse>> GetChat(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
        {
            return Unauthorized();
        }

        var chat = await _dbContext.Chats
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(c => c.Id == id && c.OwnerId == userId, cancellationToken);

        if (chat is null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(chat));
    }

    [HttpGet]
    public async Task<ActionResult<List<ChatResponse>>> GetChats(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
        {
            return Unauthorized();
        }

        var chats = await _dbContext.Chats
            .Where(c => c.OwnerId == userId)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(chats.Select(MapToResponse).ToList());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteChat(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
        {
            return Unauthorized();
        }

        var chat = await _dbContext.Chats
            .FirstOrDefaultAsync(c => c.Id == id && c.OwnerId == userId, cancellationToken);

        if (chat is null)
        {
            return NotFound();
        }

        _dbContext.Chats.Remove(chat);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Chat {ChatId} deleted for user {UserId}", id, userId);

        return NoContent();
    }

    [HttpPost("{id:guid}/messages")]
    public async Task SendMessage(
        Guid id,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var chat = await _dbContext.Chats
            .FirstOrDefaultAsync(c => c.Id == id && c.OwnerId == userId, cancellationToken);

        if (chat is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // Save original user question
        var userMessage = new MessageEntity
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Role = "user",
            Content = request.Content,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Messages.Add(userMessage);
        chat.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("User question saved to chat {ChatId}", chat.Id);

        // Set up SSE response
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var assistantContent = new StringBuilder();

        try
        {
            // Use RAG service to generate answer
            await foreach (var chunk in _ragService.GenerateAnswerAsync(
                request.Content,
                cancellationToken))
            {
                assistantContent.Append(chunk);

                // Send SSE event
                var sseData = $"data: {System.Text.Json.JsonSerializer.Serialize(new { content = chunk })}\n\n";
                await Response.WriteAsync(sseData, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }

            // Save final answer only
            var assistantMessage = new MessageEntity
            {
                Id = Guid.NewGuid(),
                ChatId = chat.Id,
                Role = "assistant",
                Content = assistantContent.ToString(),
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Messages.Add(assistantMessage);
            chat.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Assistant answer saved to chat {ChatId}", chat.Id);

            // Send completion event
            await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("SSE stream cancelled for chat {ChatId}", chat.Id);

            // Save partial response if any content was received
            if (assistantContent.Length > 0)
            {
                var assistantMessage = new MessageEntity
                {
                    Id = Guid.NewGuid(),
                    ChatId = chat.Id,
                    Role = "assistant",
                    Content = assistantContent.ToString(),
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.Messages.Add(assistantMessage);
                await _dbContext.SaveChangesAsync(CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming response for chat {ChatId}", chat.Id);

            await Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(new { error = "An error occurred while generating the response." })}\n\n", CancellationToken.None);
            await Response.Body.FlushAsync(CancellationToken.None);
        }
    }

    private static ChatResponse MapToResponse(ChatEntity chat)
    {
        return new ChatResponse
        {
            Id = chat.Id,
            Title = chat.Title,
            CreatedAt = chat.CreatedAt,
            UpdatedAt = chat.UpdatedAt,
            Messages = chat.Messages.Select(m => new MessageResponse
            {
                Id = m.Id,
                Role = m.Role,
                Content = m.Content,
                CreatedAt = m.CreatedAt
            }).ToList()
        };
    }
}
