using Microsoft.EntityFrameworkCore;

namespace ShopSphere.Services.ServiceName.Infrastructure;

public class ServiceNameDbContext : DbContext
{
    public ServiceNameDbContext(DbContextOptions<ServiceNameDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ServiceName");
        base.OnModelCreating(modelBuilder);
    }
}
