namespace Phlox.API.Entities;

public class ChatEntity
{
    public Guid Id { get; set; }
    public UserEntity Owner { get; set; }
    public string Model { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<MessageEntity> Messages { get; set; } = [];
}