using Microsoft.EntityFrameworkCore;
using ShopSphere.Services.Shipping.Domain;

namespace ShopSphere.Services.Shipping.Application.Abstractions;

public interface IShippingDbContext
{
    DbSet<Shipment> Shipments { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
