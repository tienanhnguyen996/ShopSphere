using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ShopSphere.Services.Inventory.Infrastructure;

public class InventoryDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<InventoryDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost,1433;Database=ShopSphereInventory;User Id=sa;Password=Your_password123;TrustServerCertificate=True;");
        return new InventoryDbContext(optionsBuilder.Options);
    }
}
