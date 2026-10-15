using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ShopSphere.Services.Payment.Infrastructure;

public class PaymentDbContextFactory : IDesignTimeDbContextFactory<PaymentDbContext>
{
    public PaymentDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PaymentDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost,1433;Database=ShopSpherePayment;User Id=sa;Password=Your_password123;TrustServerCertificate=True;");
        return new PaymentDbContext(optionsBuilder.Options);
    }
}
