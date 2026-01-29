using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Phlox.API.Configuration;
using Phlox.API.Services;
using Phlox.API.Tests.Fixtures;

namespace Phlox.API.Tests.Services;

public class JwtTokenServiceTests
{
    private readonly JwtOptions _jwtOptions;
    private readonly JwtTokenService _sut;

    public JwtTokenServiceTests()
    {
        _jwtOptions = new JwtOptions
        {
            Secret = "ThisIsAVeryLongSecretKeyForTestingPurposes123456",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpirationMinutes = 60
        };

        _sut = new JwtTokenService(Options.Create(_jwtOptions));
    }

    #region GenerateToken Tests

    [Fact]
    public void GenerateToken_ReturnsNonEmptyString()
    {
        // Arrange
        var user = TestDataFactory.CreateUser();

        // Act
        var result = _sut.GenerateToken(user);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateToken_ReturnsValidJwtFormat()
    {
        // Arrange
        var user = TestDataFactory.CreateUser();

        // Act
        var result = _sut.GenerateToken(user);

        // Assert
        // JWT tokens have 3 parts separated by dots
        result.Split('.').Should().HaveCount(3);
    }

    [Fact]
    public void GenerateToken_IncludesUserIdInSubClaim()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = TestDataFactory.CreateUser(id: userId);

        // Act
        var token = _sut.GenerateToken(user);

        // Assert
        var claims = DecodeToken(token);
        claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == userId.ToString());
    }

    [Fact]
    public void GenerateToken_IncludesEmailClaim()
    {
        // Arrange
        var user = TestDataFactory.CreateUser(email: "test@example.com");

        // Act
        var token = _sut.GenerateToken(user);

        // Assert
        var claims = DecodeToken(token);
        claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "test@example.com");
    }

    [Fact]
    public void GenerateToken_IncludesUsernameClaim()
    {
        // Arrange
        var user = TestDataFactory.CreateUser(username: "testuser");

        // Act
        var token = _sut.GenerateToken(user);

        // Assert
        var claims = DecodeToken(token);
        claims.Should().Contain(c => c.Type == "username" && c.Value == "testuser");
    }

    [Fact]
    public void GenerateToken_IncludesJtiClaim()
    {
        // Arrange
        var user = TestDataFactory.CreateUser();

        // Act
        var token = _sut.GenerateToken(user);

        // Assert
        var claims = DecodeToken(token);
        claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);
        var jtiClaim = claims.First(c => c.Type == JwtRegisteredClaimNames.Jti);
        Guid.TryParse(jtiClaim.Value, out _).Should().BeTrue();
    }

    [Fact]
    public void GenerateToken_IncludesNameClaimWhenNameIsSet()
    {
        // Arrange
        var user = TestDataFactory.CreateUser(name: "Test User");

        // Act
        var token = _sut.GenerateToken(user);

        // Assert
        var claims = DecodeToken(token);
        claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Name && c.Value == "Test User");
    }

    [Fact]
    public void GenerateToken_DoesNotIncludeNameClaimWhenNameIsNull()
    {
        // Arrange
        var user = TestDataFactory.CreateUser(name: null);

        // Act
        var token = _sut.GenerateToken(user);

        // Assert
        var claims = DecodeToken(token);
        var nameClaim = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Name);
        nameClaim.Should().BeNull("because user name is null");
    }

    [Fact]
    public void GenerateToken_DoesNotIncludeNameClaimWhenNameIsEmpty()
    {
        // Arrange
        var user = TestDataFactory.CreateUser(name: "");

        // Act
        var token = _sut.GenerateToken(user);

        // Assert
        var claims = DecodeToken(token);
        var nameClaim = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Name);
        nameClaim.Should().BeNull("because user name is empty");
    }

    [Fact]
    public void GenerateToken_SetsCorrectIssuer()
    {
        // Arrange
        var user = TestDataFactory.CreateUser();

        // Act
        var token = _sut.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        jwtToken.Issuer.Should().Be(_jwtOptions.Issuer);
    }

    [Fact]
    public void GenerateToken_SetsCorrectAudience()
    {
        // Arrange
        var user = TestDataFactory.CreateUser();

        // Act
        var token = _sut.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        jwtToken.Audiences.Should().Contain(_jwtOptions.Audience);
    }

    [Fact]
    public void GenerateToken_SetsCorrectExpiration()
    {
        // Arrange
        var user = TestDataFactory.CreateUser();

        // Act
        var token = _sut.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var expectedExpiration = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes);
        jwtToken.ValidTo.Should().BeCloseTo(expectedExpiration, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GenerateToken_ProducesValidSignature()
    {
        // Arrange
        var user = TestDataFactory.CreateUser();

        // Act
        var token = _sut.GenerateToken(user);

        // Assert
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _jwtOptions.Issuer,
            ValidAudience = _jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Secret))
        };

        var handler = new JwtSecurityTokenHandler();
        var validationResult = handler.ValidateToken(token, tokenValidationParameters, out _);
        validationResult.Should().NotBeNull();
    }

    [Fact]
    public void GenerateToken_ProducesUniqueJtiForEachCall()
    {
        // Arrange
        var user = TestDataFactory.CreateUser();

        // Act
        var token1 = _sut.GenerateToken(user);
        var token2 = _sut.GenerateToken(user);

        // Assert
        var claims1 = DecodeToken(token1);
        var claims2 = DecodeToken(token2);

        var jti1 = claims1.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var jti2 = claims2.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        jti1.Should().NotBe(jti2);
    }

    #endregion

    private IEnumerable<Claim> DecodeToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        return jwtToken.Claims;
    }
}
