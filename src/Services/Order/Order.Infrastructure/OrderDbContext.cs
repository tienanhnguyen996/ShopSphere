using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Idempotency;
using ShopSphere.BuildingBlocks.Outbox;
using ShopSphere.BuildingBlocks.Persistence.Extensions;
using ShopSphere.Services.Order.Application.Common.Interfaces;
using ShopSphere.Services.Order.Application.Saga;
using ShopSphere.Services.Order.Domain;

namespace ShopSphere.Services.Order.Infrastructure;

public class OrderDbContext : DbContext, IOrderDbContext
{
    public const string DefaultSchema = "order";

    public DbSet<ShopSphere.Services.Order.Domain.Order> Orders => Set<ShopSphere.Services.Order.Domain.Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<CheckoutSagaState> CheckoutSagaStates => Set<CheckoutSagaState>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(DefaultSchema);

        modelBuilder.Entity<ShopSphere.Services.Order.Domain.Order>(builder =>
        {
            builder.ToTable("Orders");
            builder.HasKey(o => o.Id);

            builder.Property(o => o.CustomerId)
                .HasMaxLength(128)
                .IsRequired();

            builder.HasIndex(o => o.CustomerId);

            builder.Property(o => o.TotalAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(o => o.Currency)
                .HasMaxLength(3)
                .IsRequired();

            builder.Property(o => o.PaymentMethod)
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(o => o.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.HasIndex(o => o.Status);

            builder.Property(o => o.PaymentAttemptId);
            builder.Property(o => o.ShipmentId);

            builder.Property(o => o.TrackingNumber)
                .HasMaxLength(100);

            builder.Property(o => o.FailureReason)
                .HasMaxLength(500);

            builder.Property(o => o.CancellationReason)
                .HasMaxLength(500);

            builder.Property(o => o.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            builder.Property(o => o.CreatedAtUtc)
                .IsRequired();

            builder.Property(o => o.UpdatedAtUtc);

            builder.HasIndex(o => o.CreatedAtUtc);

            builder.OwnsOne(o => o.ShippingAddress, address =>
            {
                address.Property(a => a.RecipientName)
                    .HasColumnName("ShippingRecipientName")
                    .HasMaxLength(150)
                    .IsRequired();

                address.Property(a => a.Street)
                    .HasColumnName("ShippingStreet")
                    .HasMaxLength(250)
                    .IsRequired();

                address.Property(a => a.City)
                    .HasColumnName("ShippingCity")
                    .HasMaxLength(100)
                    .IsRequired();

                address.Property(a => a.State)
                    .HasColumnName("ShippingState")
                    .HasMaxLength(100)
                    .IsRequired();

                address.Property(a => a.PostalCode)
                    .HasColumnName("ShippingPostalCode")
                    .HasMaxLength(30)
                    .IsRequired();

                address.Property(a => a.Country)
                    .HasColumnName("ShippingCountry")
                    .HasMaxLength(10)
                    .IsRequired();

                address.Property(a => a.Phone)
                    .HasColumnName("ShippingPhone")
                    .HasMaxLength(40)
                    .IsRequired();
            });

            builder.HasMany(o => o.Items)
                .WithOne()
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(o => o.Items)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.Ignore(o => o.DomainEvents);
        });

        modelBuilder.Entity<OrderItem>(builder =>
        {
            builder.ToTable("OrderItems");
            builder.HasKey(i => i.Id);

            builder.Property(i => i.OrderId)
                .IsRequired();

            builder.HasIndex(i => i.OrderId);

            builder.Property(i => i.Sku)
                .HasMaxLength(64)
                .IsRequired();

            builder.Property(i => i.ProductName)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(i => i.Quantity)
                .IsRequired();

            builder.Property(i => i.UnitPrice)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(i => i.LineTotal)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(i => i.Currency)
                .HasMaxLength(3)
                .IsRequired();

            builder.Property(i => i.PriceVersion)
                .IsRequired();
        });

        modelBuilder.Entity<CheckoutSagaState>(builder =>
        {
            builder.ToTable("CheckoutSagaStates");
            builder.HasKey(s => s.CorrelationId);

            builder.Property(s => s.CurrentState)
                .HasMaxLength(64)
                .IsRequired();

            builder.Property(s => s.CustomerId)
                .HasMaxLength(128)
                .IsRequired();

            builder.Property(s => s.TotalAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(s => s.Currency)
                .HasMaxLength(3)
                .IsRequired();

            builder.Property(s => s.PaymentMethod)
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(s => s.ShippingAddressJson)
                .IsRequired();

            builder.Property(s => s.ItemsJson)
                .IsRequired();

            builder.Property(s => s.TrackingNumber)
                .HasMaxLength(100);

            builder.Property(s => s.FailureReason)
                .HasMaxLength(500);

            builder.Property(s => s.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<IdempotencyRecord>(builder =>
        {
            builder.ToTable("IdempotencyRecords");
            builder.HasKey(r => r.Key);

            builder.Property(r => r.Key)
                .HasMaxLength(128)
                .IsRequired();

            builder.Property(r => r.RequestFingerprint)
                .HasMaxLength(128)
                .IsRequired();

            builder.Property(r => r.ResponseStatusCode)
                .IsRequired();

            builder.Property(r => r.ResponseBody);

            builder.Property(r => r.CreatedAtUtc)
                .IsRequired();

            builder.Property(r => r.ExpiresAtUtc)
                .IsRequired();

            builder.HasIndex(r => r.ExpiresAtUtc);
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
