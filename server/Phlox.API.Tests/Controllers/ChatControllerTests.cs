using FluentAssertions;
using Microsoft.AspNetCore.Http;
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

public class ChatControllerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly Mock<IRagService> _ragServiceMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly ChatController _sut;
    private readonly Guid _testUserId = Guid.NewGuid();

    public ChatControllerTests()
    {
        _dbContext = TestDbContextFactory.Create();
        _ragServiceMock = new Mock<IRagService>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();

        _sut = new ChatController(
            _dbContext,
            _ragServiceMock.Object,
            _currentUserServiceMock.Object,
            NullLogger<ChatController>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private void SetupAuthenticatedUser(Guid? userId = null)
    {
        _currentUserServiceMock.Setup(x => x.UserId).Returns(userId ?? _testUserId);
    }

    private void SetupUnauthenticatedUser()
    {
        _currentUserServiceMock.Setup(x => x.UserId).Returns((Guid?)null);
    }

    #region CreateChat Tests

    [Fact]
    public async Task CreateChat_WhenAuthenticated_ReturnsCreatedWithChat()
    {
        // Arrange
        SetupAuthenticatedUser();
        var request = new CreateChatRequest { Title = "Test Chat" };

        // Act
        var result = await _sut.CreateChat(request, CancellationToken.None);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = createdResult.Value.Should().BeOfType<ChatResponse>().Subject;
        response.Title.Should().Be("Test Chat");
        response.Messages.Should().BeEmpty();

        // Verify saved to database
        _dbContext.Chats.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateChat_WithNullRequest_CreatesUntitledChat()
    {
        // Arrange
        SetupAuthenticatedUser();

        // Act
        var result = await _sut.CreateChat(null, CancellationToken.None);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = createdResult.Value.Should().BeOfType<ChatResponse>().Subject;
        response.Title.Should().BeNull();
    }

    [Fact]
    public async Task CreateChat_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var request = new CreateChatRequest { Title = "Test Chat" };

        // Act
        var result = await _sut.CreateChat(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task CreateChat_SetsCorrectOwnerId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var request = new CreateChatRequest { Title = "Test Chat" };

        // Act
        await _sut.CreateChat(request, CancellationToken.None);

        // Assert
        var chat = _dbContext.Chats.Single();
        chat.OwnerId.Should().Be(userId);
    }

    #endregion

    #region GetChat Tests

    [Fact]
    public async Task GetChat_WhenChatExists_ReturnsChat()
    {
        // Arrange
        SetupAuthenticatedUser();
        var chat = TestDataFactory.CreateChat(ownerId: _testUserId, title: "My Chat");
        _dbContext.Chats.Add(chat);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetChat(chat.Id, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ChatResponse>().Subject;
        response.Id.Should().Be(chat.Id);
        response.Title.Should().Be("My Chat");
    }

    [Fact]
    public async Task GetChat_WhenChatNotFound_ReturnsNotFound()
    {
        // Arrange
        SetupAuthenticatedUser();

        // Act
        var result = await _sut.GetChat(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetChat_WhenChatBelongsToOtherUser_ReturnsNotFound()
    {
        // Arrange
        SetupAuthenticatedUser();
        var otherUserId = Guid.NewGuid();
        var chat = TestDataFactory.CreateChat(ownerId: otherUserId);
        _dbContext.Chats.Add(chat);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetChat(chat.Id, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetChat_IncludesMessages()
    {
        // Arrange
        SetupAuthenticatedUser();
        var chat = TestDataFactory.CreateChat(ownerId: _testUserId);
        var message1 = TestDataFactory.CreateMessage(chatId: chat.Id, content: "Hello");
        var message2 = TestDataFactory.CreateMessage(chatId: chat.Id, content: "Hi there", role: "assistant");
        chat.Messages.Add(message1);
        chat.Messages.Add(message2);
        _dbContext.Chats.Add(chat);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetChat(chat.Id, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ChatResponse>().Subject;
        response.Messages.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetChat_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = await _sut.GetChat(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion

    #region GetChats Tests

    [Fact]
    public async Task GetChats_ReturnsUserChats()
    {
        // Arrange
        SetupAuthenticatedUser();
        var chat1 = TestDataFactory.CreateChat(ownerId: _testUserId, title: "Chat 1");
        var chat2 = TestDataFactory.CreateChat(ownerId: _testUserId, title: "Chat 2");
        _dbContext.Chats.AddRange(chat1, chat2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetChats(1, 20, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<List<ChatResponse>>().Subject;
        response.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetChats_DoesNotReturnOtherUsersChats()
    {
        // Arrange
        SetupAuthenticatedUser();
        var myChat = TestDataFactory.CreateChat(ownerId: _testUserId, title: "My Chat");
        var otherChat = TestDataFactory.CreateChat(ownerId: Guid.NewGuid(), title: "Other Chat");
        _dbContext.Chats.AddRange(myChat, otherChat);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetChats(1, 20, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<List<ChatResponse>>().Subject;
        response.Should().HaveCount(1);
        response.Single().Title.Should().Be("My Chat");
    }

    [Fact]
    public async Task GetChats_RespectsPagination()
    {
        // Arrange
        SetupAuthenticatedUser();
        for (var i = 0; i < 30; i++)
        {
            _dbContext.Chats.Add(TestDataFactory.CreateChat(ownerId: _testUserId, title: $"Chat {i}"));
        }
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetChats(1, 10, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<List<ChatResponse>>().Subject;
        response.Should().HaveCount(10);
    }

    [Fact]
    public async Task GetChats_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = await _sut.GetChats(1, 20, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion

    #region DeleteChat Tests

    [Fact]
    public async Task DeleteChat_WhenChatExists_ReturnsNoContent()
    {
        // Arrange
        SetupAuthenticatedUser();
        var chat = TestDataFactory.CreateChat(ownerId: _testUserId);
        _dbContext.Chats.Add(chat);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.DeleteChat(chat.Id, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _dbContext.Chats.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteChat_WhenChatNotFound_ReturnsNotFound()
    {
        // Arrange
        SetupAuthenticatedUser();

        // Act
        var result = await _sut.DeleteChat(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteChat_WhenChatBelongsToOtherUser_ReturnsNotFound()
    {
        // Arrange
        SetupAuthenticatedUser();
        var chat = TestDataFactory.CreateChat(ownerId: Guid.NewGuid());
        _dbContext.Chats.Add(chat);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.DeleteChat(chat.Id, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
        _dbContext.Chats.Should().HaveCount(1);
    }

    [Fact]
    public async Task DeleteChat_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = await _sut.DeleteChat(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion
}
