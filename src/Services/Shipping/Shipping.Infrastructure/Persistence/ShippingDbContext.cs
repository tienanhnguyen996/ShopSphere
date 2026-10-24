using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Outbox;
using ShopSphere.BuildingBlocks.Persistence.Extensions;
using ShopSphere.Services.Shipping.Application.Abstractions;
using ShopSphere.Services.Shipping.Domain;

namespace ShopSphere.Services.Shipping.Infrastructure.Persistence;

public sealed class ShippingDbContext(DbContextOptions<ShippingDbContext> options)
    : DbContext(options), IShippingDbContext
{
    public const string DefaultSchema = "shipping";

    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentItem> ShipmentItems => Set<ShipmentItem>();
    public DbSet<TrackingMilestone> TrackingMilestones => Set<TrackingMilestone>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(DefaultSchema);

        modelBuilder.Entity<Shipment>(builder =>
        {
            builder.ToTable("Shipments");
            builder.HasKey(s => s.Id);

            builder.Property(s => s.OrderId).IsRequired();
            builder.Property(s => s.CustomerId).HasMaxLength(128).IsRequired();
            builder.Property(s => s.Status)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            builder.Property(s => s.Carrier).HasMaxLength(100).IsRequired();
            builder.Property(s => s.TrackingNumber).HasMaxLength(100);
            builder.Property(s => s.ExternalShipmentRequestId).HasMaxLength(128);
            builder.Property(s => s.FailureReason).HasMaxLength(500);

            builder.OwnsOne(s => s.Address, address =>
            {
                address.Property(a => a.RecipientName).HasColumnName("RecipientName").HasMaxLength(200).IsRequired();
                address.Property(a => a.Street).HasColumnName("RecipientStreet").HasMaxLength(300).IsRequired();
                address.Property(a => a.City).HasColumnName("RecipientCity").HasMaxLength(100).IsRequired();
                address.Property(a => a.State).HasColumnName("RecipientState").HasMaxLength(100).IsRequired();
                address.Property(a => a.PostalCode).HasColumnName("RecipientPostalCode").HasMaxLength(32).IsRequired();
                address.Property(a => a.Country).HasColumnName("RecipientCountry").HasMaxLength(64).IsRequired();
                address.Property(a => a.Phone).HasColumnName("RecipientPhone").HasMaxLength(32).IsRequired();
            });

            builder.Property(s => s.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            builder.HasIndex(s => s.OrderId).IsUnique();
            builder.HasIndex(s => s.TrackingNumber);

            builder.HasMany(s => s.Items)
                .WithOne()
                .HasForeignKey(i => i.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(s => s.Items)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(s => s.Milestones)
                .WithOne()
                .HasForeignKey(m => m.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(s => s.Milestones)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.Ignore(s => s.DomainEvents);
        });

        modelBuilder.Entity<ShipmentItem>(builder =>
        {
            builder.ToTable("ShipmentItems");
            builder.HasKey(i => i.Id);
            builder.Property(i => i.Id).ValueGeneratedNever();
            builder.Property(i => i.Sku).HasMaxLength(64).IsRequired();
            builder.Property(i => i.ProductName).HasMaxLength(250).IsRequired();
            builder.Property(i => i.Quantity).IsRequired();
        });

        modelBuilder.Entity<TrackingMilestone>(builder =>
        {
            builder.ToTable("TrackingMilestones");
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Id).ValueGeneratedNever();
            builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            builder.Property(m => m.Location).HasMaxLength(200);
            builder.Property(m => m.Description).HasMaxLength(500).IsRequired();
            builder.Property(m => m.OccurredAtUtc).IsRequired();
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
