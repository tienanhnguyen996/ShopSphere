using FluentAssertions;
using ShopSphere.Services.Payment.Api.Services;
using Xunit;

namespace ShopSphere.Services.Payment.Tests.Unit;

public class PaymentWebhookSignatureValidatorTests
{
    private readonly PaymentWebhookSignatureValidator _validator;
    private const string _secret = "test_secret_key_12345";

    public PaymentWebhookSignatureValidatorTests()
    {
        _validator = new PaymentWebhookSignatureValidator();
    }

    [Fact]
    public void ComputeSignature_ReturnsValidHexHmac()
    {
        // Arrange
        var payload = "{\"eventId\":\"evt_1\",\"status\":\"succeeded\"}";

        // Act
        var signature = _validator.ComputeSignature(payload, _secret);

        // Assert
        signature.Should().NotBeNullOrWhiteSpace();
        signature.Length.Should().Be(64); // SHA256 hex is 64 chars
    }

    [Fact]
    public void ValidateSignature_WithMatchingSignature_ReturnsTrue()
    {
        // Arrange
        var payload = "{\"eventId\":\"evt_1\",\"status\":\"succeeded\"}";
        var signature = _validator.ComputeSignature(payload, _secret);

        // Act
        var isValid = _validator.ValidateSignature(payload, signature, _secret);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateSignature_WithSha256Prefix_ReturnsTrue()
    {
        // Arrange
        var payload = "{\"eventId\":\"evt_2\",\"status\":\"succeeded\"}";
        var signature = "sha256=" + _validator.ComputeSignature(payload, _secret);

        // Act
        var isValid = _validator.ValidateSignature(payload, signature, _secret);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateSignature_WithMismatchedSignature_ReturnsFalse()
    {
        // Arrange
        var payload = "{\"eventId\":\"evt_1\",\"status\":\"succeeded\"}";
        var invalidSignature = "deadbeef1234567890abcdefdeadbeef1234567890abcdefdeadbeef12345678";

        // Act
        var isValid = _validator.ValidateSignature(payload, invalidSignature, _secret);

        // Assert
        isValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("", "sig", "sec")]
    [InlineData("payload", "", "sec")]
    [InlineData("payload", "sig", "")]
    public void ValidateSignature_WithEmptyInputs_ReturnsFalse(string payload, string signature, string secret)
    {
        // Act
        var isValid = _validator.ValidateSignature(payload, signature, secret);

        // Assert
        isValid.Should().BeFalse();
    }
}
