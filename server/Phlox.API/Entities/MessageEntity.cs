namespace Phlox.API.Entities;

public class MessageEntity
{
    public Guid Id { get; set; }

    public Guid ChatId { get; set; }

    public ChatEntity Chat { get; set; } = null!;

    public required string Role { get; set; }

    public required string Content { get; set; }

    public DateTime CreatedAt { get; set; }
}
