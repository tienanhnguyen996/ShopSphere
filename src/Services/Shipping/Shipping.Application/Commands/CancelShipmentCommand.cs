using Microsoft.EntityFrameworkCore;
using ShopSphere.Services.Shipping.Application.Abstractions;
using ShopSphere.Services.Shipping.Application.DTOs;
using ShopSphere.Services.Shipping.Domain.Exceptions;

namespace ShopSphere.Services.Shipping.Application.Commands;

public sealed record CancelShipmentCommand(
    Guid ShipmentId,
    string Reason);

public sealed class CancelShipmentCommandHandler(IShippingDbContext dbContext)
{
    public async Task<ShipmentDto> HandleAsync(
        CancelShipmentCommand command,
        CancellationToken cancellationToken = default)
    {
        var shipment = await dbContext.Shipments
            .Include(s => s.Items)
            .Include(s => s.Milestones)
            .FirstOrDefaultAsync(s => s.Id == command.ShipmentId, cancellationToken)
            ?? throw new ShipmentNotFoundException(command.ShipmentId);

        shipment.Cancel(command.Reason);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ShipmentDto.FromDomain(shipment);
    }
}
