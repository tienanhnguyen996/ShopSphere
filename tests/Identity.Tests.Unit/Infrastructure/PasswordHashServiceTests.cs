using FluentAssertions;
using ShopSphere.Services.Identity.Infrastructure.Services;
using Xunit;

namespace ShopSphere.Services.Identity.Tests.Unit.Infrastructure;

public class PasswordHashServiceTests
{
    private readonly PasswordHashService _service = new();

    [Fact]
    public void HashPassword_WithValidPassword_ShouldReturnSaltedHashDifferentFromPassword()
    {
        // Arrange
        var password = "SecurePassword123!";

        // Act
        var hash = _service.HashPassword(password);

        // Assert
        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().NotBe(password);
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ShouldReturnTrue()
    {
        // Arrange
        var password = "CorrectHorseBatteryStaple!";
        var hash = _service.HashPassword(password);

        // Act
        var result = _service.VerifyPassword(password, hash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WithIncorrectPassword_ShouldReturnFalse()
    {
        // Arrange
        var password = "CorrectPassword123!";
        var wrongPassword = "WrongPassword999!";
        var hash = _service.HashPassword(password);

        // Act
        var result = _service.VerifyPassword(wrongPassword, hash);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("", "hash")]
    [InlineData("password", "")]
    [InlineData(null, "hash")]
    [InlineData("password", null)]
    public void VerifyPassword_WithNullOrEmptyInputs_ShouldReturnFalse(string? password, string? hash)
    {
        // Act
        var result = _service.VerifyPassword(password!, hash!);

        // Assert
        result.Should().BeFalse();
    }
}
