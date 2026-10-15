using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Outbox;
using ShopSphere.BuildingBlocks.Persistence.Interceptors;
using ShopSphere.Services.Catalog.Domain;

namespace ShopSphere.Services.Catalog.Infrastructure;

public class CatalogDbContext : DbContext
{
    public const string DefaultSchema = "catalog";

    public DbSet<Product> Products => Set<Product>();
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
    }
}
