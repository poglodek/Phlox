namespace Phlox.API.Entities;

public class ChatEntity
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public UserEntity Owner { get; set; } = null!;

    public string? Title { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public List<MessageEntity> Messages { get; set; } = [];
}
