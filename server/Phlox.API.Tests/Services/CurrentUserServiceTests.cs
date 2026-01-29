using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Phlox.API.Services;

namespace Phlox.API.Tests.Services;

public class CurrentUserServiceTests
{
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly CurrentUserService _sut;

    public CurrentUserServiceTests()
    {
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _sut = new CurrentUserService(_httpContextAccessorMock.Object);
    }

    private void SetupAuthenticatedUser(IEnumerable<Claim> claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
    }

    private void SetupUnauthenticatedUser()
    {
        var identity = new ClaimsIdentity(); // No authentication type = not authenticated
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
    }

    private void SetupNullHttpContext()
    {
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);
    }

    #region IsAuthenticated Tests

    [Fact]
    public void IsAuthenticated_WhenUserIsAuthenticated_ReturnsTrue()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _sut.IsAuthenticated;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAuthenticated_WhenUserIsNotAuthenticated_ReturnsFalse()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = _sut.IsAuthenticated;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsAuthenticated_WhenHttpContextIsNull_ReturnsFalse()
    {
        // Arrange
        SetupNullHttpContext();

        // Act
        var result = _sut.IsAuthenticated;

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region UserId Tests

    [Fact]
    public void UserId_WhenSubClaimExists_ReturnsGuid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString())
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _sut.UserId;

        // Assert
        result.Should().Be(userId);
    }

    [Fact]
    public void UserId_WhenNameIdentifierClaimExists_ReturnsGuid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _sut.UserId;

        // Assert
        result.Should().Be(userId);
    }

    [Fact]
    public void UserId_WhenSubClaimIsInvalid_ReturnsNull()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, "not-a-guid")
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _sut.UserId;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void UserId_WhenNoUserIdClaim_ReturnsNull()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Email, "test@example.com")
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _sut.UserId;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void UserId_WhenHttpContextIsNull_ReturnsNull()
    {
        // Arrange
        SetupNullHttpContext();

        // Act
        var result = _sut.UserId;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void UserId_PrefersSubClaimOverNameIdentifier()
    {
        // Arrange
        var subUserId = Guid.NewGuid();
        var nameIdUserId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subUserId.ToString()),
            new(ClaimTypes.NameIdentifier, nameIdUserId.ToString())
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _sut.UserId;

        // Assert
        result.Should().Be(subUserId);
    }

    #endregion

    #region Email Tests

    [Fact]
    public void Email_WhenEmailClaimExists_ReturnsEmail()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Email, "test@example.com")
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _sut.Email;

        // Assert
        result.Should().Be("test@example.com");
    }

    [Fact]
    public void Email_WhenClaimTypesEmailExists_ReturnsEmail()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, "test@example.com")
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _sut.Email;

        // Assert
        result.Should().Be("test@example.com");
    }

    [Fact]
    public void Email_WhenNoEmailClaim_ReturnsNull()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _sut.Email;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Email_WhenHttpContextIsNull_ReturnsNull()
    {
        // Arrange
        SetupNullHttpContext();

        // Act
        var result = _sut.Email;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Email_PrefersJwtEmailOverClaimTypesEmail()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Email, "jwt@example.com"),
            new(ClaimTypes.Email, "claimtypes@example.com")
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _sut.Email;

        // Assert
        result.Should().Be("jwt@example.com");
    }

    #endregion

    #region Username Tests

    [Fact]
    public void Username_WhenUsernameClaimExists_ReturnsUsername()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new("username", "testuser")
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _sut.Username;

        // Assert
        result.Should().Be("testuser");
    }

    [Fact]
    public void Username_WhenClaimTypesNameExists_ReturnsUsername()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "testuser")
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _sut.Username;

        // Assert
        result.Should().Be("testuser");
    }

    [Fact]
    public void Username_WhenNoUsernameClaim_ReturnsNull()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _sut.Username;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Username_WhenHttpContextIsNull_ReturnsNull()
    {
        // Arrange
        SetupNullHttpContext();

        // Act
        var result = _sut.Username;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Username_PrefersUsernameOverClaimTypesName()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new("username", "customuser"),
            new(ClaimTypes.Name, "claimtypesuser")
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _sut.Username;

        // Assert
        result.Should().Be("customuser");
    }

    #endregion
}
