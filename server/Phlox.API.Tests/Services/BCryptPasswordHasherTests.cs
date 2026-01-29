using FluentAssertions;
using Phlox.API.Services;

namespace Phlox.API.Tests.Services;

public class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _sut;

    public BCryptPasswordHasherTests()
    {
        _sut = new BCryptPasswordHasher();
    }

    #region Hash Tests

    [Fact]
    public void Hash_ReturnsNonEmptyString()
    {
        // Act
        var result = _sut.Hash("password123");

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Hash_ReturnsDifferentHashForSamePassword()
    {
        // Act
        var hash1 = _sut.Hash("password123");
        var hash2 = _sut.Hash("password123");

        // Assert
        // BCrypt uses a random salt, so hashes should be different
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Hash_ReturnsBCryptFormattedHash()
    {
        // Act
        var result = _sut.Hash("password123");

        // Assert
        // BCrypt hashes start with $2a$, $2b$, or $2y$ followed by the cost factor
        result.Should().StartWith("$2");
        result.Should().Contain("$12$"); // Work factor is 12
    }

    [Fact]
    public void Hash_ProducesHashOfExpectedLength()
    {
        // Act
        var result = _sut.Hash("password123");

        // Assert
        // BCrypt hashes are typically 60 characters long
        result.Length.Should().Be(60);
    }

    #endregion

    #region Verify Tests

    [Fact]
    public void Verify_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        var password = "mySecurePassword123";
        var hash = _sut.Hash(password);

        // Act
        var result = _sut.Verify(password, hash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_WithIncorrectPassword_ReturnsFalse()
    {
        // Arrange
        var password = "correctPassword";
        var hash = _sut.Hash(password);

        // Act
        var result = _sut.Verify("wrongPassword", hash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_WithEmptyPassword_ReturnsFalse()
    {
        // Arrange
        var hash = _sut.Hash("password123");

        // Act
        var result = _sut.Verify("", hash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_IsCaseSensitive()
    {
        // Arrange
        var password = "Password123";
        var hash = _sut.Hash(password);

        // Act
        var result = _sut.Verify("password123", hash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_WithSpecialCharacters_Works()
    {
        // Arrange
        var password = "P@$$w0rd!#%&*()";
        var hash = _sut.Hash(password);

        // Act
        var result = _sut.Verify(password, hash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_WithUnicodeCharacters_Works()
    {
        // Arrange
        var password = "hasło123żółć";
        var hash = _sut.Hash(password);

        // Act
        var result = _sut.Verify(password, hash);

        // Assert
        result.Should().BeTrue();
    }

    #endregion
}
