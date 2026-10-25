using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShopSphere.BuildingBlocks.Messaging.Contracts;
using ShopSphere.Services.Inventory.Application.Common.Interfaces;
using ShopSphere.Services.Inventory.Application.Contracts;
using ShopSphere.Services.Inventory.Domain;

namespace ShopSphere.Services.Inventory.Application.Workers;

/// <summary>
/// Background worker that sweeps expired stock holds in discrete SQL transactions,
/// restores held quantities back to available stock, and emits outbox events.
/// </summary>
public class ReservationExpiryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReservationExpiryWorker> _logger;
    private readonly TimeSpan _interval;
    private readonly int _batchSize;

    public ReservationExpiryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ReservationExpiryWorker> logger,
        TimeSpan? interval = null,
        int batchSize = 100)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _interval = interval ?? TimeSpan.FromSeconds(30);
        _batchSize = batchSize > 0 ? batchSize : 100;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReservationExpiryWorker started with sweep interval {Interval}", _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepExpiredHoldsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred during expired reservation sweep");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("ReservationExpiryWorker stopped.");
    }

    /// <summary>
    /// Executes a single sweep cycle of overdue holds in batches.
    /// Can be called directly by unit and integration tests.
    /// </summary>
    public async Task<int> SweepExpiredHoldsAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IInventoryDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var utcNow = DateTime.UtcNow;

        var expiredReservations = await dbContext.Reservations
            .Where(r => r.Status == ReservationStatus.Held && r.ExpiresAtUtc <= utcNow)
            .OrderBy(r => r.ExpiresAtUtc)
            .Take(_batchSize)
            .ToListAsync(ct);

        if (expiredReservations.Count == 0)
        {
            return 0;
        }

        var releasedCount = 0;

        foreach (var reservation in expiredReservations)
        {
            try
            {
                var stockItem = await dbContext.StockItems
                    .Include(s => s.Reservations)
                    .FirstOrDefaultAsync(s => s.Id == reservation.StockItemId, ct);

                if (stockItem is null)
                {
                    _logger.LogWarning("StockItem {StockItemId} not found for expired reservation {ReservationId}",
                        reservation.StockItemId, reservation.Id);
                    continue;
                }

                // Restore stock bucket and transition reservation to Expired
                stockItem.ExpireHold(reservation.Id);

                var expiredEvent = new InventoryReservationExpired(
                    reservation.Id,
                    reservation.OrderId,
                    reservation.Sku,
                    reservation.Quantity,
                    reservation.ExpiresAtUtc);

                var envelope = MessageEnvelope<InventoryReservationExpired>.Create(
                    expiredEvent,
                    reservation.OrderId.ToString());

                await dbContext.SaveChangesAsync(ct);
                releasedCount++;

                await publishEndpoint.Publish(envelope, ct);
                await publishEndpoint.Publish(envelope.Payload, ct);

                _logger.LogInformation(
                    "Expired reservation {ReservationId} for Order {OrderId} (SKU: {Sku}, Quantity: {Quantity}) released successfully.",
                    reservation.Id, reservation.OrderId, reservation.Sku, reservation.Quantity);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Race condition with CommitInventory: if payment committed the hold simultaneously,
                // the rowversion changed and we gracefully skip double-release.
                _logger.LogInformation(ex,
                    "Reservation {ReservationId} was modified concurrently (likely committed). Skipping expiry release.",
                    reservation.Id);
            }
        }

        return releasedCount;
    }
}
