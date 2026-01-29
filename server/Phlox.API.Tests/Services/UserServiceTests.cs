using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Phlox.API.Data;
using Phlox.API.Services;
using Phlox.API.Tests.Fixtures;

namespace Phlox.API.Tests.Services;

public class UserServiceTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _dbContext = TestDbContextFactory.Create();
        _sut = new UserService(_dbContext, NullLogger<UserService>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WhenUserExists_ReturnsUser()
    {
        // Arrange
        var user = TestDataFactory.CreateUser(email: "test@example.com");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetByIdAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
        result.Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task GetByIdAsync_WhenUserNotFound_ReturnsNull()
    {
        // Act
        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetByEmailAsync Tests

    [Fact]
    public async Task GetByEmailAsync_WhenUserExists_ReturnsUser()
    {
        // Arrange
        var user = TestDataFactory.CreateUser(email: "test@example.com");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetByEmailAsync("test@example.com");

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task GetByEmailAsync_WhenUserNotFound_ReturnsNull()
    {
        // Act
        var result = await _sut.GetByEmailAsync("nonexistent@example.com");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByEmailAsync_IsCaseSensitive()
    {
        // Arrange
        var user = TestDataFactory.CreateUser(email: "Test@Example.com");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetByEmailAsync("test@example.com");

        // Assert
        // Note: EF Core InMemory provider is case-sensitive by default
        result.Should().BeNull();
    }

    #endregion

    #region GetByUsernameAsync Tests

    [Fact]
    public async Task GetByUsernameAsync_WhenUserExists_ReturnsUser()
    {
        // Arrange
        var user = TestDataFactory.CreateUser(username: "testuser");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetByUsernameAsync("testuser");

        // Assert
        result.Should().NotBeNull();
        result!.Username.Should().Be("testuser");
    }

    [Fact]
    public async Task GetByUsernameAsync_WhenUserNotFound_ReturnsNull()
    {
        // Act
        var result = await _sut.GetByUsernameAsync("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetByEmailOrUsernameAsync Tests

    [Fact]
    public async Task GetByEmailOrUsernameAsync_WithEmail_ReturnsUser()
    {
        // Arrange
        var user = TestDataFactory.CreateUser(email: "test@example.com", username: "testuser");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetByEmailOrUsernameAsync("test@example.com");

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task GetByEmailOrUsernameAsync_WithUsername_ReturnsUser()
    {
        // Arrange
        var user = TestDataFactory.CreateUser(email: "test@example.com", username: "testuser");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetByEmailOrUsernameAsync("testuser");

        // Assert
        result.Should().NotBeNull();
        result!.Username.Should().Be("testuser");
    }

    [Fact]
    public async Task GetByEmailOrUsernameAsync_WhenNotFound_ReturnsNull()
    {
        // Act
        var result = await _sut.GetByEmailOrUsernameAsync("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region ExistsByEmailAsync Tests

    [Fact]
    public async Task ExistsByEmailAsync_WhenExists_ReturnsTrue()
    {
        // Arrange
        var user = TestDataFactory.CreateUser(email: "existing@example.com");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.ExistsByEmailAsync("existing@example.com");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByEmailAsync_WhenNotExists_ReturnsFalse()
    {
        // Act
        var result = await _sut.ExistsByEmailAsync("nonexistent@example.com");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ExistsByUsernameAsync Tests

    [Fact]
    public async Task ExistsByUsernameAsync_WhenExists_ReturnsTrue()
    {
        // Arrange
        var user = TestDataFactory.CreateUser(username: "existinguser");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.ExistsByUsernameAsync("existinguser");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByUsernameAsync_WhenNotExists_ReturnsFalse()
    {
        // Act
        var result = await _sut.ExistsByUsernameAsync("nonexistent");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_CreatesNewUser()
    {
        // Act
        var result = await _sut.CreateAsync(
            "new@example.com",
            "newuser",
            "hashedpassword",
            "New User");

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be("new@example.com");
        result.Username.Should().Be("newuser");
        result.PasswordHash.Should().Be("hashedpassword");
        result.Name.Should().Be("New User");
        result.IsActive.Should().BeTrue();
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateAsync_SavesUserToDatabase()
    {
        // Act
        var result = await _sut.CreateAsync(
            "new@example.com",
            "newuser",
            "hashedpassword",
            null);

        // Assert
        _dbContext.Users.Should().HaveCount(1);
        var savedUser = _dbContext.Users.Single();
        savedUser.Id.Should().Be(result.Id);
    }

    [Fact]
    public async Task CreateAsync_GeneratesUniqueId()
    {
        // Act
        var user1 = await _sut.CreateAsync("user1@example.com", "user1", "hash", null);
        var user2 = await _sut.CreateAsync("user2@example.com", "user2", "hash", null);

        // Assert
        user1.Id.Should().NotBe(user2.Id);
        user1.Id.Should().NotBe(Guid.Empty);
        user2.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateAsync_SetsLastLoginToCreationTime()
    {
        // Act
        var result = await _sut.CreateAsync(
            "new@example.com",
            "newuser",
            "hashedpassword",
            null);

        // Assert
        result.LastLoginAt.Should().BeCloseTo(result.CreatedAt, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region UpdateLastLoginAsync Tests

    [Fact(Skip = "InMemory provider does not support ExecuteUpdateAsync")]
    public async Task UpdateLastLoginAsync_UpdatesTimestamp()
    {
        // Arrange
        var originalTime = DateTime.UtcNow.AddDays(-1);
        var user = TestDataFactory.CreateUser();
        user.LastLoginAt = originalTime;
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.UpdateLastLoginAsync(user.Id);

        // Assert
        // Clear change tracker to get fresh data
        _dbContext.ChangeTracker.Clear();
        var updatedUser = await _dbContext.Users.FindAsync(user.Id);
        updatedUser!.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        updatedUser.LastLoginAt.Should().NotBe(originalTime);
    }

    #endregion
}
