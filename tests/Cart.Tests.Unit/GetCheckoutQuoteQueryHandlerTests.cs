using FluentAssertions;
using FluentValidation;
using Moq;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Cart.Application.Carts.Queries.GetCheckoutQuote;
using ShopSphere.Services.Cart.Application.Common.Interfaces;
using ShopSphere.Services.Cart.Domain;
using Xunit;
using CartAggregate = ShopSphere.Services.Cart.Domain.Cart;

namespace ShopSphere.Services.Cart.Tests.Unit;

public class GetCheckoutQuoteQueryHandlerTests
{
    private readonly Mock<ICartRepository> _cartRepositoryMock;
    private readonly Mock<ICatalogClient> _catalogClientMock;
    private readonly IValidator<GetCheckoutQuoteQuery> _validator;
    private readonly GetCheckoutQuoteQueryHandler _handler;

    public GetCheckoutQuoteQueryHandlerTests()
    {
        _cartRepositoryMock = new Mock<ICartRepository>();
        _catalogClientMock = new Mock<ICatalogClient>();
        _validator = new GetCheckoutQuoteQueryValidator();
        _handler = new GetCheckoutQuoteQueryHandler(_cartRepositoryMock.Object, _catalogClientMock.Object, _validator);
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerIdEmpty_ShouldReturnValidationFailure()
    {
        // Arrange
        var query = new GetCheckoutQuoteQuery("");

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task HandleAsync_WhenCartIsEmpty_ShouldReturnValidEmptyQuote()
    {
        // Arrange
        var customerId = "cust-1";
        var cart = new CartAggregate(customerId);
        _cartRepositoryMock
            .Setup(r => r.GetCartAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        // Act
        var result = await _handler.HandleAsync(new GetCheckoutQuoteQuery(customerId));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var quote = result.Value;
        quote.CustomerId.Should().Be(customerId);
        quote.Items.Should().BeEmpty();
        quote.Subtotal.Should().Be(0m);
        quote.IsValid.Should().BeTrue();
        quote.ValidationErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenAllItemsActive_ShouldReturnValidQuoteWithCalculatedSubtotalAndPriceVersions()
    {
        // Arrange
        var customerId = "cust-2";
        var cart = new CartAggregate(customerId);
        cart.AddItem("KB-001", 2);
        cart.AddItem("MOU-001", 1);

        _cartRepositoryMock
            .Setup(r => r.GetCartAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var kbProduct = new CatalogProductDto(Guid.NewGuid(), "Mechanical Keyboard", "RGB", "KB-001", 100m, "USD", "Active", 3);
        var mouProduct = new CatalogProductDto(Guid.NewGuid(), "Wireless Mouse", "Fast", "MOU-001", 50m, "USD", "Active", 1);

        _catalogClientMock
            .Setup(c => c.GetProductBySkuAsync("KB-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(kbProduct));

        _catalogClientMock
            .Setup(c => c.GetProductBySkuAsync("MOU-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(mouProduct));

        // Act
        var result = await _handler.HandleAsync(new GetCheckoutQuoteQuery(customerId));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var quote = result.Value;
        quote.IsValid.Should().BeTrue();
        quote.ValidationErrors.Should().BeEmpty();
        quote.Subtotal.Should().Be(250m); // 2 * 100 + 1 * 50
        quote.Items.Should().HaveCount(2);

        quote.PriceVersions.Should().ContainKey("KB-001").WhoseValue.Should().Be(3);
        quote.PriceVersions.Should().ContainKey("MOU-001").WhoseValue.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_WhenItemDiscontinued_ShouldMarkQuoteInvalidWithError()
    {
        // Arrange
        var customerId = "cust-3";
        var cart = new CartAggregate(customerId);
        cart.AddItem("DISC-001", 1);

        _cartRepositoryMock
            .Setup(r => r.GetCartAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var discProduct = new CatalogProductDto(Guid.NewGuid(), "Old Mouse", "Old", "DISC-001", 30m, "USD", "Discontinued", 1);

        _catalogClientMock
            .Setup(c => c.GetProductBySkuAsync("DISC-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(discProduct));

        // Act
        var result = await _handler.HandleAsync(new GetCheckoutQuoteQuery(customerId));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var quote = result.Value;
        quote.IsValid.Should().BeFalse();
        quote.ValidationErrors.Should().Contain(e => e.Contains("DISC-001") && e.Contains("Discontinued"));
        quote.Subtotal.Should().Be(0m);
    }

    [Fact]
    public async Task HandleAsync_WhenItemNotFoundInCatalog_ShouldMarkQuoteInvalid()
    {
        // Arrange
        var customerId = "cust-4";
        var cart = new CartAggregate(customerId);
        cart.AddItem("GHOST-001", 1);

        _cartRepositoryMock
            .Setup(r => r.GetCartAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        _catalogClientMock
            .Setup(c => c.GetProductBySkuAsync("GHOST-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CatalogProductDto>(Error.NotFound("Catalog.ProductNotFound", "Product not found")));

        // Act
        var result = await _handler.HandleAsync(new GetCheckoutQuoteQuery(customerId));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var quote = result.Value;
        quote.IsValid.Should().BeFalse();
        quote.ValidationErrors.Should().Contain(e => e.Contains("GHOST-001") && e.Contains("not found"));
    }

    [Fact]
    public async Task HandleAsync_WhenCatalogServiceFailsOrTimesOut_ShouldReturnServiceUnavailableFailure()
    {
        // Arrange
        var customerId = "cust-5";
        var cart = new CartAggregate(customerId);
        cart.AddItem("KB-001", 1);

        _cartRepositoryMock
            .Setup(r => r.GetCartAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        _catalogClientMock
            .Setup(c => c.GetProductBySkuAsync("KB-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CatalogProductDto>(
                Error.Failure("Cart.CatalogUnavailable", "Catalog service is currently unavailable.")));

        // Act
        var result = await _handler.HandleAsync(new GetCheckoutQuoteQuery(customerId));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Cart.CatalogUnavailable");
        result.Error.Type.Should().Be(ErrorType.Failure);
    }
}
