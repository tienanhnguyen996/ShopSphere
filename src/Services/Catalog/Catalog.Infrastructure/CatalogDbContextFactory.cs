using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ShopSphere.Services.Catalog.Infrastructure;

public class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CatalogDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost,1433;Database=ShopSphereCatalog;User Id=sa;Password=Your_password123;TrustServerCertificate=True;");
        return new CatalogDbContext(optionsBuilder.Options);
    }
}
