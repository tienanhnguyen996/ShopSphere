using Microsoft.EntityFrameworkCore;
using ShopSphere.Services.Shipping.Application.Abstractions;
using ShopSphere.Services.Shipping.Application.DTOs;

namespace ShopSphere.Services.Shipping.Application.Queries;

public sealed record GetShipmentByIdQuery(Guid ShipmentId);
public sealed record GetShipmentByOrderIdQuery(Guid OrderId);
public sealed record GetShipmentByTrackingNumberQuery(string TrackingNumber);

public sealed class GetShipmentQueriesHandler(IShippingDbContext dbContext)
{
    public async Task<ShipmentDto?> HandleAsync(GetShipmentByIdQuery query, CancellationToken cancellationToken = default)
    {
        var shipment = await dbContext.Shipments
            .AsNoTracking()
            .Include(s => s.Items)
            .Include(s => s.Milestones)
            .FirstOrDefaultAsync(s => s.Id == query.ShipmentId, cancellationToken);

        return shipment is null ? null : ShipmentDto.FromDomain(shipment);
    }

    public async Task<ShipmentDto?> HandleAsync(GetShipmentByOrderIdQuery query, CancellationToken cancellationToken = default)
    {
        var shipment = await dbContext.Shipments
            .AsNoTracking()
            .Include(s => s.Items)
            .Include(s => s.Milestones)
            .FirstOrDefaultAsync(s => s.OrderId == query.OrderId, cancellationToken);

        return shipment is null ? null : ShipmentDto.FromDomain(shipment);
    }

    public async Task<ShipmentDto?> HandleAsync(GetShipmentByTrackingNumberQuery query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.TrackingNumber))
        {
            return null;
        }

        var normalized = query.TrackingNumber.Trim();
        var shipment = await dbContext.Shipments
            .AsNoTracking()
            .Include(s => s.Items)
            .Include(s => s.Milestones)
            .FirstOrDefaultAsync(s => s.TrackingNumber == normalized, cancellationToken);

        return shipment is null ? null : ShipmentDto.FromDomain(shipment);
    }
}
