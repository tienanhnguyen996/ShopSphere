using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ShopSphere.Services.Notification.Infrastructure.Persistence;

public sealed class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__NotificationDb")
            ?? "Server=localhost,1433;Database=ShopSphere_Notification;User Id=sa;Password=ShopSphere_DevPassword123!;TrustServerCertificate=True;Encrypt=False";

        var optionsBuilder = new DbContextOptionsBuilder<NotificationDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsHistoryTable("__EFMigrationsHistory", NotificationDbContext.DefaultSchema);
        });

        return new NotificationDbContext(optionsBuilder.Options);
    }
}
