using ShopSphere.BuildingBlocks.Common.Domain;
using ShopSphere.Services.Order.Domain.Events;
using ShopSphere.Services.Order.Domain.Exceptions;

namespace ShopSphere.Services.Order.Domain;

/// <summary>
/// Aggregate root representing a customer order and its verified checkout price snapshot.
/// Enforces strict state machine transitions and late-event compensation guards.
/// </summary>
public class Order : AggregateRoot<Guid>, IVersionedEntity, IAuditableEntity
{
    private readonly List<OrderItem> _items = new();

    public string CustomerId { get; private set; } = string.Empty;
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
    public ShippingAddress ShippingAddress { get; private set; } = default!;
    public decimal TotalAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string PaymentMethod { get; private set; } = string.Empty;
    public OrderStatus Status { get; private set; }
    public Guid? PaymentAttemptId { get; private set; }
    public Guid? ShipmentId { get; private set; }
    public string? TrackingNumber { get; private set; }
    public string? FailureReason { get; private set; }
    public string? CancellationReason { get; private set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    private Order() { }

    private Order(
        Guid id,
        string customerId,
        ShippingAddress shippingAddress,
        string paymentMethod,
        IReadOnlyList<OrderItem> items) : base(id)
    {
        if (id == Guid.Empty)
        {
            throw new OrderDomainException("Order ID cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new OrderDomainException("Customer ID is required.");
        }

        if (shippingAddress is null)
        {
            throw new OrderDomainException("Shipping address is required.");
        }

        if (string.IsNullOrWhiteSpace(paymentMethod))
        {
            throw new OrderDomainException("Payment method is required.");
        }

        if (items is null || items.Count == 0)
        {
            throw new OrderDomainException("An order must contain at least one item.");
        }

        var currency = items[0].Currency;
        if (items.Any(i => !string.Equals(i.Currency, currency, StringComparison.OrdinalIgnoreCase)))
        {
            throw new OrderDomainException("All order items must use the same currency.");
        }

        CustomerId = customerId.Trim();
        ShippingAddress = shippingAddress;
        PaymentMethod = paymentMethod.Trim().ToLowerInvariant();
        Currency = currency.ToUpperInvariant();
        _items.AddRange(items);
        TotalAmount = _items.Sum(i => i.LineTotal);
        Status = OrderStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new OrderCreatedDomainEvent(Id, CustomerId, TotalAmount, Currency, PaymentMethod));
    }

    public static Order Create(
        Guid id,
        string customerId,
        ShippingAddress shippingAddress,
        string paymentMethod,
        IEnumerable<OrderItem> items)
    {
        var itemList = items?.ToList() ?? [];
        return new Order(id, customerId, shippingAddress, paymentMethod, itemList);
    }

    public void MarkReserving()
    {
        if (Status == OrderStatus.Reserving)
        {
            return;
        }

        if (Status != OrderStatus.Pending)
        {
            throw new InvalidOrderStateTransitionException(
                Status,
                OrderStatus.Reserving,
                $"Cannot transition order {Id} from {Status} to {OrderStatus.Reserving}.");
        }

        var previous = Status;
        Status = OrderStatus.Reserving;
        UpdatedAtUtc = DateTime.UtcNow;
        AddDomainEvent(new OrderStatusChangedDomainEvent(Id, previous, Status));
    }

    public void MarkPaying()
    {
        if (Status == OrderStatus.Paying)
        {
            return;
        }

        if (Status is not (OrderStatus.Pending or OrderStatus.Reserving))
        {
            throw new InvalidOrderStateTransitionException(
                Status,
                OrderStatus.Paying,
                $"Cannot transition order {Id} from {Status} to {OrderStatus.Paying}.");
        }

        var previous = Status;
        Status = OrderStatus.Paying;
        UpdatedAtUtc = DateTime.UtcNow;
        AddDomainEvent(new OrderStatusChangedDomainEvent(Id, previous, Status));
    }

    public void MarkPaymentUnknown(Guid paymentAttemptId, string reason)
    {
        if (paymentAttemptId == Guid.Empty)
        {
            throw new OrderDomainException("PaymentAttemptId is required when marking payment unknown.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new OrderDomainException("Reason is required when marking payment unknown.");
        }

        if (Status is OrderStatus.Confirmed or OrderStatus.Cancelled or OrderStatus.Refunding)
        {
            throw new InvalidOrderStateTransitionException(
                Status,
                OrderStatus.PaymentUnknown,
                $"Cannot transition order {Id} from terminal state {Status} to {OrderStatus.PaymentUnknown}.");
        }

        var previous = Status;
        Status = OrderStatus.PaymentUnknown;
        PaymentAttemptId = paymentAttemptId;
        FailureReason = reason.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
        AddDomainEvent(new OrderStatusChangedDomainEvent(Id, previous, Status, FailureReason));
    }

    public void MarkPaymentConfirmed(Guid paymentAttemptId)
    {
        if (paymentAttemptId == Guid.Empty)
        {
            throw new OrderDomainException("PaymentAttemptId is required.");
        }

        if (Status is OrderStatus.Cancelled or OrderStatus.Refunding)
        {
            throw new InvalidOrderStateTransitionException(
                Status,
                OrderStatus.Paying,
                $"Cannot confirm payment on order {Id} in state {Status}.");
        }

        PaymentAttemptId = paymentAttemptId;
        FailureReason = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkConfirmed(Guid? shipmentId = null, string? trackingNumber = null)
    {
        if (Status == OrderStatus.Confirmed)
        {
            if (shipmentId.HasValue && shipmentId.Value != Guid.Empty)
            {
                ShipmentId = shipmentId.Value;
            }

            if (!string.IsNullOrWhiteSpace(trackingNumber))
            {
                TrackingNumber = trackingNumber.Trim();
            }

            return;
        }

        if (Status is OrderStatus.Cancelled or OrderStatus.Refunding)
        {
            throw new InvalidOrderStateTransitionException(
                Status,
                OrderStatus.Confirmed,
                $"Cannot confirm order {Id} because it is already in {Status} state.");
        }

        var previous = Status;
        Status = OrderStatus.Confirmed;
        if (shipmentId.HasValue && shipmentId.Value != Guid.Empty)
        {
            ShipmentId = shipmentId.Value;
        }

        if (!string.IsNullOrWhiteSpace(trackingNumber))
        {
            TrackingNumber = trackingNumber.Trim();
        }

        FailureReason = null;
        UpdatedAtUtc = DateTime.UtcNow;
        AddDomainEvent(new OrderStatusChangedDomainEvent(Id, previous, Status));
    }

    public void MarkRefunding(string reason, Guid? paymentAttemptId = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new OrderDomainException("Reason is required when transitioning to Refunding.");
        }

        if (Status == OrderStatus.Refunding)
        {
            return;
        }

        // Allowed from Paying, PaymentUnknown, Confirmed, or Cancelled (when late payment arrives on a cancelled order)
        if (Status is OrderStatus.Pending or OrderStatus.Reserving)
        {
            throw new InvalidOrderStateTransitionException(
                Status,
                OrderStatus.Refunding,
                $"Cannot transition order {Id} from {Status} to {OrderStatus.Refunding} before payment stage.");
        }

        var previous = Status;
        Status = OrderStatus.Refunding;
        if (paymentAttemptId.HasValue && paymentAttemptId.Value != Guid.Empty)
        {
            PaymentAttemptId = paymentAttemptId.Value;
        }

        FailureReason = reason.Trim();
        CancellationReason = reason.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
        AddDomainEvent(new OrderStatusChangedDomainEvent(Id, previous, Status, CancellationReason));
    }

    public void MarkCancelled(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new OrderDomainException("Cancellation reason is required.");
        }

        if (Status == OrderStatus.Cancelled)
        {
            return;
        }

        if (Status == OrderStatus.Confirmed)
        {
            throw new InvalidOrderStateTransitionException(
                Status,
                OrderStatus.Cancelled,
                $"Cannot directly cancel confirmed order {Id} without refunding.");
        }

        var previous = Status;
        Status = OrderStatus.Cancelled;
        CancellationReason = reason.Trim();
        FailureReason ??= reason.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
        AddDomainEvent(new OrderStatusChangedDomainEvent(Id, previous, Status, CancellationReason));
    }
}
