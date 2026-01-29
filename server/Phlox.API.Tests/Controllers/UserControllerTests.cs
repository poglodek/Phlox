using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phlox.API.Controllers;
using Phlox.API.DTOs;
using Phlox.API.Entities;
using Phlox.API.Services;
using Phlox.API.Tests.Fixtures;

namespace Phlox.API.Tests.Controllers;

public class UserControllerTests
{
    private readonly Mock<IUserService> _userServiceMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly UserController _sut;

    public UserControllerTests()
    {
        _userServiceMock = new Mock<IUserService>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();

        _sut = new UserController(
            _userServiceMock.Object,
            _currentUserServiceMock.Object);
    }

    [Fact]
    public async Task GetCurrentUser_WhenAuthenticated_ReturnsOkWithUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = TestDataFactory.CreateUser(
            id: userId,
            email: "test@example.com",
            username: "testuser",
            name: "Test User");

        _currentUserServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        _currentUserServiceMock.Setup(x => x.UserId).Returns(userId);
        _userServiceMock.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.GetCurrentUser(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<UserResponse>().Subject;
        response.Id.Should().Be(userId);
        response.Email.Should().Be("test@example.com");
        response.Username.Should().Be("testuser");
        response.Name.Should().Be("Test User");
    }

    [Fact]
    public async Task GetCurrentUser_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _currentUserServiceMock.Setup(x => x.IsAuthenticated).Returns(false);

        // Act
        var result = await _sut.GetCurrentUser(CancellationToken.None);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().BeEquivalentTo(new { message = "User is not authenticated" });
    }

    [Fact]
    public async Task GetCurrentUser_WhenUserIdIsNull_ReturnsUnauthorized()
    {
        // Arrange
        _currentUserServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        _currentUserServiceMock.Setup(x => x.UserId).Returns((Guid?)null);

        // Act
        var result = await _sut.GetCurrentUser(CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task GetCurrentUser_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _currentUserServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        _currentUserServiceMock.Setup(x => x.UserId).Returns(userId);
        _userServiceMock.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        // Act
        var result = await _sut.GetCurrentUser(CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().BeEquivalentTo(new { message = "User not found" });
    }

    [Fact]
    public async Task GetCurrentUser_IncludesAllUserFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddDays(-30);
        var lastLoginAt = DateTime.UtcNow.AddHours(-1);

        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = "hash",
            Name = "Test User",
            CreatedAt = createdAt,
            LastLoginAt = lastLoginAt,
            IsActive = true
        };

        _currentUserServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        _currentUserServiceMock.Setup(x => x.UserId).Returns(userId);
        _userServiceMock.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.GetCurrentUser(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<UserResponse>().Subject;
        response.Id.Should().Be(userId);
        response.Email.Should().Be("test@example.com");
        response.Username.Should().Be("testuser");
        response.Name.Should().Be("Test User");
        response.CreatedAt.Should().Be(createdAt);
        response.LastLoginAt.Should().Be(lastLoginAt);
        response.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetCurrentUser_WhenUserInactive_StillReturnsUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = TestDataFactory.CreateUser(id: userId, isActive: false);

        _currentUserServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        _currentUserServiceMock.Setup(x => x.UserId).Returns(userId);
        _userServiceMock.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.GetCurrentUser(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<UserResponse>().Subject;
        response.IsActive.Should().BeFalse();
    }
}
