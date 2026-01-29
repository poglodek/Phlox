using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Phlox.API.Configuration;
using Phlox.API.Controllers;
using Phlox.API.DTOs;
using Phlox.API.Entities;
using Phlox.API.Services;
using Phlox.API.Tests.Fixtures;

namespace Phlox.API.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IUserService> _userServiceMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly IOptions<JwtOptions> _jwtOptions;
    private readonly AuthController _sut;

    public AuthControllerTests()
    {
        _userServiceMock = new Mock<IUserService>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _tokenServiceMock = new Mock<ITokenService>();
        _jwtOptions = Options.Create(new JwtOptions
        {
            Secret = "test-secret-key-with-minimum-length-for-testing",
            Issuer = "test-issuer",
            Audience = "test-audience",
            ExpirationMinutes = 60
        });

        _sut = new AuthController(
            _userServiceMock.Object,
            _passwordHasherMock.Object,
            _tokenServiceMock.Object,
            _jwtOptions,
            NullLogger<AuthController>.Instance);
    }

    #region Register Tests

    [Fact]
    public async Task Register_WithValidRequest_ReturnsOkWithToken()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Username = "testuser",
            Password = "password123",
            Name = "Test User"
        };

        var user = TestDataFactory.CreateUser(
            email: request.Email,
            username: request.Username,
            name: request.Name);

        _userServiceMock.Setup(x => x.ExistsByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userServiceMock.Setup(x => x.ExistsByUsernameAsync(request.Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _passwordHasherMock.Setup(x => x.Hash(request.Password))
            .Returns("hashed_password");
        _userServiceMock.Setup(x => x.CreateAsync(
                request.Email, request.Username, "hashed_password", request.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _tokenServiceMock.Setup(x => x.GenerateToken(user))
            .Returns("jwt_token");

        // Act
        var result = await _sut.Register(request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<AuthResponse>().Subject;
        response.Token.Should().Be("jwt_token");
        response.Email.Should().Be(request.Email);
        response.Username.Should().Be(request.Username);
    }

    [Fact]
    public async Task Register_WithExistingEmail_ReturnsConflict()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "existing@example.com",
            Username = "newuser",
            Password = "password123"
        };

        _userServiceMock.Setup(x => x.ExistsByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.Register(request, CancellationToken.None);

        // Assert
        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.Value.Should().BeEquivalentTo(new { message = "Email is already registered" });
    }

    [Fact]
    public async Task Register_WithExistingUsername_ReturnsConflict()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "new@example.com",
            Username = "existinguser",
            Password = "password123"
        };

        _userServiceMock.Setup(x => x.ExistsByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userServiceMock.Setup(x => x.ExistsByUsernameAsync(request.Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.Register(request, CancellationToken.None);

        // Assert
        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.Value.Should().BeEquivalentTo(new { message = "Username is already taken" });
    }

    [Fact]
    public async Task Register_HashesPassword()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Username = "testuser",
            Password = "mypassword123"
        };

        var user = TestDataFactory.CreateUser();

        _userServiceMock.Setup(x => x.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userServiceMock.Setup(x => x.ExistsByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userServiceMock.Setup(x => x.CreateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _tokenServiceMock.Setup(x => x.GenerateToken(It.IsAny<UserEntity>()))
            .Returns("token");

        // Act
        await _sut.Register(request, CancellationToken.None);

        // Assert
        _passwordHasherMock.Verify(x => x.Hash("mypassword123"), Times.Once);
    }

    #endregion

    #region Login Tests

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithToken()
    {
        // Arrange
        var request = new LoginRequest
        {
            EmailOrUsername = "test@example.com",
            Password = "password123"
        };

        var user = TestDataFactory.CreateUser(email: request.EmailOrUsername);

        _userServiceMock.Setup(x => x.GetByEmailOrUsernameAsync(request.EmailOrUsername, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(x => x.Verify(request.Password, user.PasswordHash))
            .Returns(true);
        _tokenServiceMock.Setup(x => x.GenerateToken(user))
            .Returns("jwt_token");

        // Act
        var result = await _sut.Login(request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<AuthResponse>().Subject;
        response.Token.Should().Be("jwt_token");
    }

    [Fact]
    public async Task Login_WithInvalidUser_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest
        {
            EmailOrUsername = "nonexistent@example.com",
            Password = "password123"
        };

        _userServiceMock.Setup(x => x.GetByEmailOrUsernameAsync(request.EmailOrUsername, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        // Act
        var result = await _sut.Login(request, CancellationToken.None);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().BeEquivalentTo(new { message = "Invalid credentials" });
    }

    [Fact]
    public async Task Login_WithInactiveUser_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest
        {
            EmailOrUsername = "inactive@example.com",
            Password = "password123"
        };

        var user = TestDataFactory.CreateUser(email: request.EmailOrUsername, isActive: false);

        _userServiceMock.Setup(x => x.GetByEmailOrUsernameAsync(request.EmailOrUsername, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.Login(request, CancellationToken.None);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().BeEquivalentTo(new { message = "Account is deactivated" });
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest
        {
            EmailOrUsername = "test@example.com",
            Password = "wrongpassword"
        };

        var user = TestDataFactory.CreateUser(email: request.EmailOrUsername);

        _userServiceMock.Setup(x => x.GetByEmailOrUsernameAsync(request.EmailOrUsername, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(x => x.Verify(request.Password, user.PasswordHash))
            .Returns(false);

        // Act
        var result = await _sut.Login(request, CancellationToken.None);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().BeEquivalentTo(new { message = "Invalid credentials" });
    }

    [Fact]
    public async Task Login_UpdatesLastLoginTimestamp()
    {
        // Arrange
        var request = new LoginRequest
        {
            EmailOrUsername = "test@example.com",
            Password = "password123"
        };

        var user = TestDataFactory.CreateUser(email: request.EmailOrUsername);

        _userServiceMock.Setup(x => x.GetByEmailOrUsernameAsync(request.EmailOrUsername, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(x => x.Verify(request.Password, user.PasswordHash))
            .Returns(true);
        _tokenServiceMock.Setup(x => x.GenerateToken(user))
            .Returns("token");

        // Act
        await _sut.Login(request, CancellationToken.None);

        // Assert
        _userServiceMock.Verify(x => x.UpdateLastLoginAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Login_WithUsername_ReturnsOkWithToken()
    {
        // Arrange
        var request = new LoginRequest
        {
            EmailOrUsername = "testuser",
            Password = "password123"
        };

        var user = TestDataFactory.CreateUser(username: request.EmailOrUsername);

        _userServiceMock.Setup(x => x.GetByEmailOrUsernameAsync(request.EmailOrUsername, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(x => x.Verify(request.Password, user.PasswordHash))
            .Returns(true);
        _tokenServiceMock.Setup(x => x.GenerateToken(user))
            .Returns("jwt_token");

        // Act
        var result = await _sut.Login(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion
}
