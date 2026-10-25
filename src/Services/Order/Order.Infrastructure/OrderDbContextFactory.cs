using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ShopSphere.Services.Order.Infrastructure;

public class OrderDbContextFactory : IDesignTimeDbContextFactory<OrderDbContext>
{
    public OrderDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrderDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__OrderDatabase")
            ?? "Server=localhost,1433;Database=ShopSphereOrders;User Id=sa;Password=DevPassword123!;TrustServerCertificate=True;";

        optionsBuilder.UseSqlServer(connectionString);
        return new OrderDbContext(optionsBuilder.Options);
    }
}
