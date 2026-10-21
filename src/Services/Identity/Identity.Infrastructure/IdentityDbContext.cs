using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Outbox;
using ShopSphere.BuildingBlocks.Persistence.Extensions;
using ShopSphere.Services.Identity.Application.Common.Interfaces;
using ShopSphere.Services.Identity.Domain;

namespace ShopSphere.Services.Identity.Infrastructure;

public class IdentityDbContext : DbContext, IIdentityDbContext
{
    public const string DefaultSchema = "identity";

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(DefaultSchema);

        modelBuilder.Entity<User>(builder =>
        {
            builder.ToTable("Users");
            builder.HasKey(u => u.Id);

            builder.Property(u => u.Email)
                .HasMaxLength(256)
                .IsRequired();

            builder.HasIndex(u => u.Email)
                .IsUnique();

            builder.Property(u => u.PasswordHash)
                .HasMaxLength(500)
                .IsRequired();

            builder.Property(u => u.FullName)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(u => u.IsActive)
                .IsRequired();

            builder.Property(u => u.CreatedAtUtc)
                .IsRequired();

            builder.Property(u => u.UpdatedAtUtc);

            builder.HasMany(u => u.UserRoles)
                .WithOne(ur => ur.User)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(u => u.UserRoles)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Role>(builder =>
        {
            builder.ToTable("Roles");
            builder.HasKey(r => r.Id);

            builder.Property(r => r.Name)
                .HasMaxLength(100)
                .IsRequired();

            builder.HasIndex(r => r.Name)
                .IsUnique();

            builder.Property(r => r.Description)
                .HasMaxLength(500);

            builder.HasMany(r => r.RolePermissions)
                .WithOne(rp => rp.Role)
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(r => r.RolePermissions)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Permission>(builder =>
        {
            builder.ToTable("Permissions");
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Code)
                .HasMaxLength(100)
                .IsRequired();

            builder.HasIndex(p => p.Code)
                .IsUnique();

            builder.Property(p => p.Description)
                .HasMaxLength(500);
        });

        modelBuilder.Entity<UserRole>(builder =>
        {
            builder.ToTable("UserRoles");
            builder.HasKey(ur => new { ur.UserId, ur.RoleId });

            builder.HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(ur => ur.Role)
                .WithMany()
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RolePermission>(builder =>
        {
            builder.ToTable("RolePermissions");
            builder.HasKey(rp => new { rp.RoleId, rp.PermissionId });

            builder.HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(rp => rp.Permission)
                .WithMany()
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(builder =>
        {
            builder.ToTable("RefreshTokens");
            builder.HasKey(rt => rt.Id);

            builder.Property(rt => rt.UserId)
                .IsRequired();

            builder.Property(rt => rt.TokenHash)
                .HasMaxLength(256)
                .IsRequired();

            builder.Property(rt => rt.ExpiresAtUtc)
                .IsRequired();

            builder.Property(rt => rt.CreatedAtUtc)
                .IsRequired();

            builder.Property(rt => rt.RevokedAtUtc);
            builder.Property(rt => rt.ReplacedByTokenId);

            builder.HasIndex(rt => rt.TokenHash)
                .IsUnique();

            builder.HasIndex(rt => rt.UserId);

            builder.Ignore(rt => rt.IsActive);
            builder.Ignore(rt => rt.IsExpired);
            builder.Ignore(rt => rt.IsRevoked);
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
