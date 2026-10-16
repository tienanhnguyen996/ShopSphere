using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ShopSphere.Services.Identity.Infrastructure;

public class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost,1433;Database=ShopSphereIdentity;User Id=sa;Password=Your_password123;TrustServerCertificate=True;");
        return new IdentityDbContext(optionsBuilder.Options);
    }
}
