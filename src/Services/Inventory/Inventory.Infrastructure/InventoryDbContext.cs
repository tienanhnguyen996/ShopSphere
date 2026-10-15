using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Outbox;
using ShopSphere.BuildingBlocks.Persistence.Extensions;
using ShopSphere.Services.Inventory.Application.Common.Interfaces;
using ShopSphere.Services.Inventory.Domain;

namespace ShopSphere.Services.Inventory.Infrastructure;

public class InventoryDbContext : DbContext, IInventoryDbContext
{
    public const string DefaultSchema = "inventory";

    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<StockReservation> Reservations => Set<StockReservation>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(DefaultSchema);

        modelBuilder.Entity<StockItem>(builder =>
        {
            builder.ToTable("StockItems");
            builder.HasKey(s => s.Id);

            builder.Property(s => s.Sku)
                .HasMaxLength(50)
                .IsRequired();

            builder.HasIndex(s => s.Sku)
                .IsUnique();

            builder.Property(s => s.QuantityAvailable)
                .IsRequired();

            builder.Property(s => s.QuantityHeld)
                .IsRequired();

            builder.Property(s => s.QuantityCommitted)
                .IsRequired();

            builder.Property(s => s.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            builder.Property(s => s.CreatedAtUtc)
                .IsRequired();

            builder.Property(s => s.UpdatedAtUtc);

            builder.HasMany(s => s.Reservations)
                .WithOne(r => r.StockItem)
                .HasForeignKey(r => r.StockItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StockReservation>(builder =>
        {
            builder.ToTable("Reservations");
            builder.HasKey(r => r.Id);

            builder.Property(r => r.StockItemId)
                .IsRequired();

            builder.Property(r => r.OrderId)
                .IsRequired();

            builder.Property(r => r.Sku)
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(r => r.Quantity)
                .IsRequired();

            builder.Property(r => r.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(r => r.ExpiresAtUtc)
                .IsRequired();

            builder.Property(r => r.CreatedAtUtc)
                .IsRequired();

            builder.Property(r => r.UpdatedAtUtc);

            builder.HasIndex(r => new { r.OrderId, r.Sku })
                .IsUnique();

            builder.HasIndex(r => new { r.Status, r.ExpiresAtUtc });
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
