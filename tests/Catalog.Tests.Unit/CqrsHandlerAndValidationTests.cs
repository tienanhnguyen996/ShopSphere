using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.BuildingBlocks.Outbox;
using ShopSphere.Services.Catalog.Application.Common.Interfaces;
using ShopSphere.Services.Catalog.Application.Products.Commands.CreateProduct;
using ShopSphere.Services.Catalog.Application.Products.Commands.UpdatePrice;
using ShopSphere.Services.Catalog.Application.Products.Queries.GetProductBySku;
using ShopSphere.Services.Catalog.Application.Products.Queries.GetProducts;
using ShopSphere.Services.Catalog.Domain;
using ShopSphere.Services.Catalog.Tests.Unit.Helpers;

namespace ShopSphere.Services.Catalog.Tests.Unit;

public class CqrsHandlerAndValidationTests
{
    private readonly CreateProductCommandValidator _createValidator = new();
    private readonly UpdateProductPriceCommandValidator _updatePriceValidator = new();
    private readonly GetProductBySkuQueryValidator _getBySkuValidator = new();
    private readonly GetProductsQueryValidator _getProductsValidator = new();

    #region CreateProduct Tests

    [Fact]
    public void CreateProductCommandValidator_WithValidData_ShouldPass()
    {
        // Arrange
        var command = new CreateProductCommand(
            "Mechanical Keyboard",
            "RGB Wireless keyboard",
            "KB-RGB-001",
            99.99m,
            "USD");

        // Act
        var result = _createValidator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Product name cannot be empty.")]
    [InlineData("   ", "Product name cannot be empty.")]
    public void CreateProductCommandValidator_WithEmptyName_ShouldFailWithPropertyName(string name, string expectedMessage)
    {
        // Arrange
        var command = new CreateProductCommand(name, "Desc", "SKU-001", 50m, "USD");

        // Act
        var result = _createValidator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProductCommand.Name) && e.ErrorMessage == expectedMessage);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("SKU@123")]
    [InlineData("SKU WITH SPACE")]
    public void CreateProductCommandValidator_WithInvalidSku_ShouldFail(string invalidSku)
    {
        // Arrange
        var command = new CreateProductCommand("Name", "Desc", invalidSku, 50m, "USD");

        // Act
        var result = _createValidator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProductCommand.Sku));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void CreateProductCommandValidator_WithNonPositivePrice_ShouldFail(decimal price)
    {
        // Arrange
        var command = new CreateProductCommand("Name", "Desc", "SKU-001", price, "USD");

        // Act
        var result = _createValidator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProductCommand.PriceAmount));
    }

    [Fact]
    public async Task CreateProductCommandHandler_WithDuplicateSku_ShouldReturnConflictError()
    {
        // Arrange
        var existingProduct = Product.Create(
            Guid.NewGuid(),
            "Existing Keyboard",
            "Desc",
            new Sku("KB-RGB-001"),
            new Money(99.99m, "USD"));

        var productsMock = MockDbSetExtensions.CreateMockDbSet(new List<Product> { existingProduct });
        var dbContextMock = new Mock<ICatalogDbContext>();
        dbContextMock.Setup(x => x.Products).Returns(productsMock.Object);

        var handler = new CreateProductCommandHandler(dbContextMock.Object, _createValidator);
        var command = new CreateProductCommand("New Keyboard", "Desc", "KB-RGB-001", 89.99m, "USD");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Product.DuplicateSku");
    }

    [Fact]
    public async Task CreateProductCommandHandler_WithValidCommand_ShouldCreateProductAndReturnDto()
    {
        // Arrange
        var products = new List<Product>();
        var productsMock = MockDbSetExtensions.CreateMockDbSet(products);
        var dbContextMock = new Mock<ICatalogDbContext>();
        dbContextMock.Setup(x => x.Products).Returns(productsMock.Object);
        dbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new CreateProductCommandHandler(dbContextMock.Object, _createValidator);
        var command = new CreateProductCommand(
            "New Wireless Mouse",
            "Ergonomic sensor mouse",
            "MS-WLS-002",
            49.99m,
            "USD",
            ActivateImmediately: true);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be("New Wireless Mouse");
        result.Value.Sku.Should().Be("MS-WLS-002");
        result.Value.Price.Should().Be(49.99m);
        result.Value.Status.Should().Be(ProductStatus.Active.ToString());

        dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region UpdateProductPrice Tests

    [Fact]
    public void UpdateProductPriceCommandValidator_WithValidData_ShouldPass()
    {
        // Arrange
        var command = new UpdateProductPriceCommand("KB-RGB-001", 119.99m, "USD");

        // Act
        var result = _updatePriceValidator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void UpdateProductPriceCommandValidator_WithNonPositivePrice_ShouldFail(decimal price)
    {
        // Arrange
        var command = new UpdateProductPriceCommand("KB-RGB-001", price);

        // Act
        var result = _updatePriceValidator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateProductPriceCommand.NewPrice));
    }

    [Fact]
    public async Task UpdateProductPriceCommandHandler_WhenProductNotFound_ShouldReturnNotFoundError()
    {
        // Arrange
        var productsMock = MockDbSetExtensions.CreateMockDbSet(new List<Product>());
        var dbContextMock = new Mock<ICatalogDbContext>();
        dbContextMock.Setup(x => x.Products).Returns(productsMock.Object);

        var handler = new UpdateProductPriceCommandHandler(dbContextMock.Object, _updatePriceValidator);
        var command = new UpdateProductPriceCommand("NONEXISTENT", 99.99m);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Product.NotFound");
    }

    [Fact]
    public async Task UpdateProductPriceCommandHandler_WithValidPrice_ShouldUpdatePriceSaveHistoryAndOutboxMessage()
    {
        // Arrange
        var product = Product.Create(
            Guid.NewGuid(),
            "Mechanical Keyboard",
            "Desc",
            new Sku("KB-RGB-001"),
            new Money(100m, "USD"));

        var productsMock = MockDbSetExtensions.CreateMockDbSet(new List<Product> { product });
        var priceHistories = new List<PriceHistory>();
        var priceHistoriesMock = MockDbSetExtensions.CreateMockDbSet(priceHistories);
        var outboxMessages = new List<OutboxMessage>();
        var outboxMessagesMock = MockDbSetExtensions.CreateMockDbSet(outboxMessages);

        var dbContextMock = new Mock<ICatalogDbContext>();
        dbContextMock.Setup(x => x.Products).Returns(productsMock.Object);
        dbContextMock.Setup(x => x.PriceHistories).Returns(priceHistoriesMock.Object);
        dbContextMock.Setup(x => x.OutboxMessages).Returns(outboxMessagesMock.Object);
        dbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdateProductPriceCommandHandler(dbContextMock.Object, _updatePriceValidator);
        var command = new UpdateProductPriceCommand("KB-RGB-001", 125m, "USD");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Price.Should().Be(125m);
        result.Value.PriceVersion.Should().Be(2);

        // Verify PriceHistory was added in the same transaction
        priceHistories.Should().ContainSingle(h =>
            h.ProductId == product.Id &&
            h.Sku == "KB-RGB-001" &&
            h.OldPriceAmount == 100m &&
            h.NewPriceAmount == 125m &&
            h.PriceVersion == 2);

        // Verify OutboxMessage was added in the same transaction
        outboxMessages.Should().ContainSingle(o =>
            o.Type.Contains("ProductPriceChangedDomainEvent") &&
            o.Content.Contains("125"));

        dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Query Tests

    [Fact]
    public async Task GetProductBySkuQueryHandler_WhenNotFound_ShouldReturnNotFoundError()
    {
        // Arrange
        var productsMock = MockDbSetExtensions.CreateMockDbSet(new List<Product>());
        var dbContextMock = new Mock<ICatalogDbContext>();
        dbContextMock.Setup(x => x.Products).Returns(productsMock.Object);

        var handler = new GetProductBySkuQueryHandler(dbContextMock.Object, _getBySkuValidator);
        var query = new GetProductBySkuQuery("MISSING-SKU");

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetProductBySkuQueryHandler_WhenFound_ShouldReturnProductDto()
    {
        // Arrange
        var product = Product.Create(
            Guid.NewGuid(),
            "Mechanical Keyboard",
            "Desc",
            new Sku("KB-RGB-001"),
            new Money(99.99m, "USD"));

        var productsMock = MockDbSetExtensions.CreateMockDbSet(new List<Product> { product });
        var dbContextMock = new Mock<ICatalogDbContext>();
        dbContextMock.Setup(x => x.Products).Returns(productsMock.Object);

        var handler = new GetProductBySkuQueryHandler(dbContextMock.Object, _getBySkuValidator);
        var query = new GetProductBySkuQuery("KB-RGB-001");

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Sku.Should().Be("KB-RGB-001");
        result.Value.Price.Should().Be(99.99m);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void GetProductsQueryValidator_WithInvalidPagination_ShouldFail(int page, int pageSize)
    {
        // Arrange
        var query = new GetProductsQuery(page, pageSize);

        // Act
        var result = _getProductsValidator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task GetProductsQueryHandler_ShouldReturnPagedActiveProducts()
    {
        // Arrange
        var p1 = Product.Create(Guid.NewGuid(), "Keyboard", "Desc", new Sku("KB-001"), new Money(100m, "USD"));
        p1.Activate();
        var p2 = Product.Create(Guid.NewGuid(), "Mouse", "Desc", new Sku("MS-002"), new Money(50m, "USD"));
        p2.Activate();
        var p3 = Product.Create(Guid.NewGuid(), "Draft Item", "Desc", new Sku("DF-003"), new Money(10m, "USD")); // Draft

        var productsMock = MockDbSetExtensions.CreateMockDbSet(new List<Product> { p1, p2, p3 });
        var dbContextMock = new Mock<ICatalogDbContext>();
        dbContextMock.Setup(x => x.Products).Returns(productsMock.Object);

        var handler = new GetProductsQueryHandler(dbContextMock.Object, _getProductsValidator);
        var query = new GetProductsQuery(Page: 1, PageSize: 10, OnlyActive: true);

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(2);
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Should().OnlyContain(p => p.Status == ProductStatus.Active.ToString());
        result.Value.TotalPages.Should().Be(1);
        result.Value.HasNextPage.Should().BeFalse();
        result.Value.HasPreviousPage.Should().BeFalse();
    }

    #endregion
}
