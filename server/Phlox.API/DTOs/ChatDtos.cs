namespace Phlox.API.DTOs;

public class CreateChatRequest
{
    public string? Title { get; set; }
}

public class ChatResponse
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<MessageResponse> Messages { get; set; } = [];
}

public class MessageResponse
{
    public Guid Id { get; set; }
    public required string Role { get; set; }
    public required string Content { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SendMessageRequest
{
    public required string Content { get; set; }
}
