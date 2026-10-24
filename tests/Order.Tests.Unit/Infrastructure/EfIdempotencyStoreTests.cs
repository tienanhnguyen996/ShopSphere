using FluentAssertions;
using Moq;
using ShopSphere.BuildingBlocks.Idempotency;
using ShopSphere.Services.Order.Application.Common.Interfaces;
using ShopSphere.Services.Order.Infrastructure.Idempotency;
using ShopSphere.Services.Order.Tests.Unit.Helpers;
using Xunit;

namespace ShopSphere.Services.Order.Tests.Unit.Infrastructure;

public class EfIdempotencyStoreTests
{
    [Fact]
    public async Task SaveAndGetAsync_WhenRecordIsUnexpired_ShouldReturnStoredRecord()
    {
        // Arrange
        var records = new List<IdempotencyRecord>();
        var mockSet = MockDbSetExtensions.CreateMockDbSet(records);
        var dbContextMock = new Mock<IOrderDbContext>();
        dbContextMock.Setup(d => d.IdempotencyRecords).Returns(mockSet.Object);

        var store = new EfIdempotencyStore(dbContextMock.Object);
        var record = new IdempotencyRecord
        {
            Key = "idem-key-101",
            RequestFingerprint = "hash-abc",
            ResponseStatusCode = 201,
            ResponseBody = "{\"orderId\":\"123\"}",
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(24)
        };

        // Act
        await store.SaveAsync(record);
        var retrieved = await store.GetAsync("idem-key-101");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Key.Should().Be("idem-key-101");
        retrieved.RequestFingerprint.Should().Be("hash-abc");
        retrieved.ResponseStatusCode.Should().Be(201);
        dbContextMock.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_WhenRecordIsExpired_ShouldReturnNull()
    {
        // Arrange
        var records = new List<IdempotencyRecord>
        {
            new()
            {
                Key = "expired-key",
                RequestFingerprint = "hash-old",
                ResponseStatusCode = 201,
                ResponseBody = "{}",
                CreatedAtUtc = DateTime.UtcNow.AddHours(-48),
                ExpiresAtUtc = DateTime.UtcNow.AddHours(-24)
            }
        };
        var mockSet = MockDbSetExtensions.CreateMockDbSet(records);
        var dbContextMock = new Mock<IOrderDbContext>();
        dbContextMock.Setup(d => d.IdempotencyRecords).Returns(mockSet.Object);

        var store = new EfIdempotencyStore(dbContextMock.Object);

        // Act
        var retrieved = await store.GetAsync("expired-key");

        // Assert
        retrieved.Should().BeNull();
    }
}
