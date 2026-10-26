using Microsoft.EntityFrameworkCore;
using ShopSphere.Services.Inventory.Domain;

namespace ShopSphere.Services.Inventory.Infrastructure;

public static class InventoryDbSeeder
{
    private static readonly (string Sku, int InitialQty)[] _defaultSkus =
    [
        ("KB-RGB-001", 100),
        ("MS-WLS-002", 100),
        ("HS-GAM-003", 100),
        ("MN-4K-004", 100),
        ("KB-ERG-01", 24),
        ("MS-WLS-02", 15),
        ("MN-OLED-27", 11),
        ("DK-TB4-04", 10)
    ];

    public static async Task SeedAsync(InventoryDbContext dbContext, CancellationToken ct = default)
    {
        var existingSkus = await dbContext.StockItems
            .Select(s => s.Sku)
            .ToListAsync(ct);

        var existingSet = new HashSet<string>(existingSkus, StringComparer.OrdinalIgnoreCase);
        var added = false;

        foreach (var (sku, qty) in _defaultSkus)
        {
            if (!existingSet.Contains(sku))
            {
                dbContext.StockItems.Add(StockItem.Create(sku, qty));
                added = true;
            }
        }

        if (added)
        {
            await dbContext.SaveChangesAsync(ct);
        }
    }
}
