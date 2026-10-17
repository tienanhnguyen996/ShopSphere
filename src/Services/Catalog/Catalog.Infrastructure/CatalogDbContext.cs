using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Outbox;
using ShopSphere.BuildingBlocks.Persistence.Extensions;
using ShopSphere.Services.Catalog.Domain;

using ShopSphere.Services.Catalog.Application.Common.Interfaces;

namespace ShopSphere.Services.Catalog.Infrastructure;

public class CatalogDbContext : DbContext, ICatalogDbContext
{
    public const string DefaultSchema = "catalog";

    public DbSet<Product> Products => Set<Product>();
    public DbSet<PriceHistory> PriceHistories => Set<PriceHistory>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(DefaultSchema);

        modelBuilder.Entity<Product>(builder =>
        {
            builder.ToTable("Products");
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Name)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(p => p.Description)
                .HasMaxLength(2000)
                .HasDefaultValue(string.Empty);

            builder.Property(p => p.Sku)
                .HasConversion(sku => sku.Value, value => new Sku(value))
                .HasMaxLength(50)
                .IsRequired();

            builder.HasIndex(p => p.Sku)
                .IsUnique();

            builder.OwnsOne(p => p.Price, priceBuilder =>
            {
                priceBuilder.Property(m => m.Amount)
                    .HasColumnName("PriceAmount")
                    .HasPrecision(18, 2)
                    .IsRequired();

                priceBuilder.Property(m => m.Currency)
                    .HasColumnName("PriceCurrency")
                    .HasMaxLength(3)
                    .IsRequired();
            });

            builder.Property(p => p.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(p => p.PriceVersion)
                .IsRequired()
                .HasDefaultValue(1);

            builder.Property(p => p.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            builder.Property(p => p.CreatedAtUtc)
                .IsRequired();

            builder.Property(p => p.UpdatedAtUtc);
        });

        modelBuilder.Entity<PriceHistory>(builder =>
        {
            builder.ToTable("PriceHistory");
            builder.HasKey(h => h.Id);

            builder.Property(h => h.ProductId)
                .IsRequired();

            builder.Property(h => h.Sku)
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(h => h.OldPriceAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(h => h.NewPriceAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(h => h.Currency)
                .HasMaxLength(3)
                .IsRequired();

            builder.Property(h => h.PriceVersion)
                .IsRequired();

            builder.Property(h => h.ChangedAtUtc)
                .IsRequired();

            builder.HasIndex(h => h.ProductId);
            builder.HasIndex(h => h.Sku);
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
