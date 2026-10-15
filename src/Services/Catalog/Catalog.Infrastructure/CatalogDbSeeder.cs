using Microsoft.EntityFrameworkCore;
using ShopSphere.Services.Catalog.Domain;

namespace ShopSphere.Services.Catalog.Infrastructure;

public static class CatalogDbSeeder
{
    public static readonly Guid KeyboardId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid MouseId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid HeadsetId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid MonitorId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public static async Task SeedAsync(CatalogDbContext dbContext, CancellationToken ct = default)
    {
        if (!await dbContext.Products.AnyAsync(ct))
        {
            var p1 = Product.Create(
                KeyboardId,
                "Mechanical Gaming Keyboard",
                "RGB Wireless mechanical keyboard with hot-swappable switches",
                new Sku("KB-RGB-001"),
                new Money(99.99m, "USD"));
            p1.Activate();

            var p2 = Product.Create(
                MouseId,
                "Wireless Ergonomic Mouse",
                "High-precision 16,000 DPI sensor with silent click buttons",
                new Sku("MS-WLS-002"),
                new Money(49.99m, "USD"));
            p2.Activate();

            var p3 = Product.Create(
                HeadsetId,
                "Noise Cancelling Headset",
                "Spatial audio gaming headset with detachable boom mic",
                new Sku("HS-GAM-003"),
                new Money(79.99m, "USD"));
            p3.Activate();

            var p4 = Product.Create(
                MonitorId,
                "UltraWide 4K Gaming Monitor",
                "144Hz 1ms IPS gaming display with HDR600 support",
                new Sku("MN-4K-004"),
                new Money(299.99m, "USD"));
            p4.Activate();

            dbContext.Products.AddRange(p1, p2, p3, p4);
            await dbContext.SaveChangesAsync(ct);
        }
    }
}
