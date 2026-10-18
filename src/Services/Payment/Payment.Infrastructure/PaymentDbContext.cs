using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Outbox;
using ShopSphere.BuildingBlocks.Persistence.Extensions;
using ShopSphere.Services.Payment.Application.Common.Interfaces;
using ShopSphere.Services.Payment.Domain;

namespace ShopSphere.Services.Payment.Infrastructure;

public class PaymentDbContext : DbContext, IPaymentDbContext
{
    public const string DefaultSchema = "payment";

    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();
    public DbSet<PaymentWebhookLog> WebhookLogs => Set<PaymentWebhookLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(DefaultSchema);

        modelBuilder.Entity<PaymentAttempt>(builder =>
        {
            builder.ToTable("PaymentAttempts");
            builder.HasKey(p => p.Id);

            builder.Property(p => p.OrderId)
                .IsRequired();

            builder.HasIndex(p => p.OrderId);

            builder.Property(p => p.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(p => p.Currency)
                .HasMaxLength(3)
                .IsRequired();

            builder.Property(p => p.PaymentMethod)
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(p => p.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.HasIndex(p => p.Status);

            builder.Property(p => p.ProviderTransactionId)
                .HasMaxLength(100);

            builder.HasIndex(p => p.ProviderTransactionId);

            builder.Property(p => p.FailureReason)
                .HasMaxLength(500);

            builder.Property(p => p.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            builder.Property(p => p.CreatedAtUtc)
                .IsRequired();

            builder.Property(p => p.UpdatedAtUtc);

            builder.Ignore(p => p.DomainEvents);
        });

        modelBuilder.Entity<PaymentWebhookLog>(builder =>
        {
            builder.ToTable("WebhookLogs");
            builder.HasKey(w => w.Id);

            builder.Property(w => w.EventId)
                .HasMaxLength(100)
                .IsRequired();

            builder.HasIndex(w => w.EventId)
                .IsUnique();

            builder.Property(w => w.EventType)
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(w => w.Payload)
                .IsRequired();

            builder.Property(w => w.ProcessedAtUtc)
                .IsRequired();
        });

        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("OutboxMessages");
            builder.HasKey(o => o.Id);

            builder.Property(o => o.Type)
                .HasMaxLength(256)
                .IsRequired();

            builder.Property(o => o.Content)
                .IsRequired();

            builder.Property(o => o.OccurredAtUtc)
                .IsRequired();

            builder.Property(o => o.ProcessedAtUtc);
            builder.Property(o => o.Error);

            builder.HasIndex(o => o.ProcessedAtUtc);
        });

        modelBuilder.AddShopSphereOutboxEntities();
    }
}
