using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ShopSphere.Services.Catalog.Domain;
using ShopSphere.Services.Catalog.Infrastructure;

namespace ShopSphere.Services.Catalog.Tests.Unit;

public class CatalogDbContextTests
{
    private readonly CatalogDbContext _context;

    public CatalogDbContextTests()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer("Server=localhost;Database=TestCatalog;User Id=sa;Password=Password123!;TrustServerCertificate=True;")
            .Options;

        _context = new CatalogDbContext(options);
    }

    [Fact]
    public void Model_ShouldUseCatalogDefaultSchema()
    {
        // Assert
        _context.Model.GetDefaultSchema().Should().Be("catalog");
    }

    [Fact]
    public void ProductEntity_ShouldBeMappedToCatalogProductsTable()
    {
        // Arrange
        var entityType = _context.Model.FindEntityType(typeof(Product))!;

        // Assert
        entityType.Should().NotBeNull();
        entityType.GetTableName().Should().Be("Products");
        entityType.GetSchema().Should().Be("catalog");
    }

    [Fact]
    public void ProductEntity_ShouldHaveUniqueIndexOnSku()
    {
        // Arrange
        var entityType = _context.Model.FindEntityType(typeof(Product))!;
        var skuProperty = entityType.FindProperty(nameof(Product.Sku))!;

        // Assert
        skuProperty.Should().NotBeNull();
        var index = entityType.FindIndex(skuProperty);
        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void ProductEntity_ShouldHaveRowVersionAsConcurrencyToken()
    {
        // Arrange
        var entityType = _context.Model.FindEntityType(typeof(Product))!;
        var rowVersionProperty = entityType.FindProperty(nameof(Product.RowVersion))!;

        // Assert
        rowVersionProperty.Should().NotBeNull();
        rowVersionProperty.IsConcurrencyToken.Should().BeTrue();
        rowVersionProperty.ValueGenerated.Should().Be(ValueGenerated.OnAddOrUpdate);
    }

    [Fact]
    public void ProductEntity_ShouldHaveOwnedPriceWithAmountAndCurrency()
    {
        // Arrange
        var entityType = _context.Model.FindEntityType(typeof(Product))!;
        var priceNav = entityType.FindNavigation(nameof(Product.Price))!;

        // Assert
        priceNav.Should().NotBeNull();
        var priceType = priceNav.TargetEntityType;
        priceType.IsOwned().Should().BeTrue();

        var amountProp = priceType.FindProperty(nameof(Money.Amount))!;
        amountProp.Should().NotBeNull();
        amountProp.GetColumnName().Should().Be("PriceAmount");
        amountProp.GetPrecision().Should().Be(18);
        amountProp.GetScale().Should().Be(2);

        var currencyProp = priceType.FindProperty(nameof(Money.Currency))!;
        currencyProp.Should().NotBeNull();
        currencyProp.GetColumnName().Should().Be("PriceCurrency");
        currencyProp.GetMaxLength().Should().Be(3);
    }

    [Fact]
    public void PriceHistoryEntity_ShouldBeMappedWithAppropriateIndexes()
    {
        // Arrange
        var entityType = _context.Model.FindEntityType(typeof(PriceHistory))!;

        // Assert
        entityType.Should().NotBeNull();
        entityType.GetTableName().Should().Be("PriceHistory");
        entityType.GetSchema().Should().Be("catalog");

        var productIdProp = entityType.FindProperty(nameof(PriceHistory.ProductId))!;
        var productIndex = entityType.FindIndex(productIdProp);
        productIndex.Should().NotBeNull();

        var skuProp = entityType.FindProperty(nameof(PriceHistory.Sku))!;
        var skuIndex = entityType.FindIndex(skuProp);
        skuIndex.Should().NotBeNull();
    }

    [Fact]
    public void Model_ShouldIncludeMassTransitOutboxEntities()
    {
        // Arrange
        var inboxState = _context.Model.FindEntityType("MassTransit.EntityFrameworkCoreIntegration.InboxState");
        var outboxMessage = _context.Model.FindEntityType("MassTransit.EntityFrameworkCoreIntegration.OutboxMessage");
        var outboxState = _context.Model.FindEntityType("MassTransit.EntityFrameworkCoreIntegration.OutboxState");

        // Assert
        inboxState.Should().NotBeNull();
        outboxMessage.Should().NotBeNull();
        outboxState.Should().NotBeNull();
    }
}
