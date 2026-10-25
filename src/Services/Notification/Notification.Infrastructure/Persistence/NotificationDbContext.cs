using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Outbox;
using ShopSphere.BuildingBlocks.Persistence.Extensions;
using ShopSphere.Services.Notification.Application.Abstractions;
using ShopSphere.Services.Notification.Domain;

namespace ShopSphere.Services.Notification.Infrastructure.Persistence;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options)
    : DbContext(options), INotificationDbContext
{
    public const string DefaultSchema = "notification";

    public DbSet<NotificationJob> NotificationJobs => Set<NotificationJob>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(DefaultSchema);

        modelBuilder.Entity<NotificationJob>(builder =>
        {
            builder.ToTable("NotificationJobs");
            builder.HasKey(j => j.Id);

            builder.Property(j => j.OrderId).IsRequired();
            builder.Property(j => j.CustomerId).HasMaxLength(128).IsRequired();
            builder.Property(j => j.NotificationType).HasConversion<string>().HasMaxLength(32).IsRequired();
            builder.Property(j => j.Channel).HasConversion<string>().HasMaxLength(16).IsRequired();
            builder.Property(j => j.Recipient).HasMaxLength(256).IsRequired();
            builder.Property(j => j.Subject).HasMaxLength(256).IsRequired();
            builder.Property(j => j.Body).HasMaxLength(2000).IsRequired();
            builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            builder.Property(j => j.SourceMessageId).IsRequired();
            builder.Property(j => j.RetryCount).IsRequired();
            builder.Property(j => j.ProviderReferenceId).HasMaxLength(128);
            builder.Property(j => j.LastError).HasMaxLength(500);

            builder.Property(j => j.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            builder.HasIndex(j => new { j.OrderId, j.NotificationType, j.Channel }).IsUnique();
            builder.HasIndex(j => j.OrderId);

            builder.Ignore(j => j.DomainEvents);
        });

        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("OutboxMessages");
            builder.HasKey(o => o.Id);
            builder.Property(o => o.Type).HasMaxLength(256).IsRequired();
            builder.Property(o => o.Content).IsRequired();
            builder.Property(o => o.OccurredAtUtc).IsRequired();
            builder.Property(o => o.ProcessedAtUtc);
            builder.Property(o => o.Error);
            builder.HasIndex(o => o.ProcessedAtUtc);
        });

        modelBuilder.AddShopSphereOutboxEntities();
    }
}
