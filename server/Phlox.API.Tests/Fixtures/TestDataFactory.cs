using Phlox.API.Entities;

namespace Phlox.API.Tests.Fixtures;

public static class TestDataFactory
{
    public static UserEntity CreateUser(
        Guid? id = null,
        string email = "test@example.com",
        string username = "testuser",
        string passwordHash = "hashedpassword",
        string? name = "Test User",
        bool isActive = true)
    {
        return new UserEntity
        {
            Id = id ?? Guid.NewGuid(),
            Email = email,
            Username = username,
            PasswordHash = passwordHash,
            Name = name,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsActive = isActive
        };
    }

    public static ChatEntity CreateChat(
        Guid? id = null,
        Guid? ownerId = null,
        string? title = "Test Chat")
    {
        return new ChatEntity
        {
            Id = id ?? Guid.NewGuid(),
            OwnerId = ownerId ?? Guid.NewGuid(),
            Title = title,
            CreatedAt = DateTime.UtcNow,
            Messages = []
        };
    }

    public static MessageEntity CreateMessage(
        Guid? id = null,
        Guid? chatId = null,
        string role = "user",
        string content = "Test message")
    {
        return new MessageEntity
        {
            Id = id ?? Guid.NewGuid(),
            ChatId = chatId ?? Guid.NewGuid(),
            Role = role,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static DocumentEntity CreateDocument(
        Guid? id = null,
        string title = "Test Document",
        string content = "Test content")
    {
        return new DocumentEntity
        {
            Id = id ?? Guid.NewGuid(),
            Title = title,
            Content = content,
            CreatedAt = DateTime.UtcNow,
            Paragraphs = []
        };
    }

    public static ParagraphEntity CreateParagraph(
        Guid? id = null,
        Guid? documentId = null,
        int index = 0,
        string content = "Test paragraph content")
    {
        return new ParagraphEntity
        {
            Id = id ?? Guid.NewGuid(),
            DocumentId = documentId ?? Guid.NewGuid(),
            Index = index,
            Content = content
        };
    }
}
