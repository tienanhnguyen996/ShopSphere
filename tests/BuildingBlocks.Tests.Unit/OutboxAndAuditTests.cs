using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShopSphere.BuildingBlocks.Common.Domain;
using ShopSphere.BuildingBlocks.Persistence.Extensions;
using ShopSphere.BuildingBlocks.Persistence.Interceptors;
using Xunit;

namespace ShopSphere.BuildingBlocks.Tests.Unit;

public class TestAuditableEntity : Entity<Guid>, IAuditableEntity
{
    public TestAuditableEntity(Guid id, string name) : base(id)
    {
        Name = name;
    }

    public string Name { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public class TestAppDbContext : DbContext
{
    public TestAppDbContext(DbContextOptions<TestAppDbContext> options) : base(options) { }

    public DbSet<TestAuditableEntity> AuditableEntities => Set<TestAuditableEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.AddShopSphereOutboxEntities();
    }
}

public class OutboxAndAuditTests
{
    [Fact]
    public void ModelBuilder_AddShopSphereOutboxEntities_RegistersMassTransitEntities()
    {
        var options = new DbContextOptionsBuilder<TestAppDbContext>()
            .UseSqlServer("Server=dummy;Database=dummy;TrustServerCertificate=True;")
            .Options;

        using var context = new TestAppDbContext(options);
        var entityTypes = context.Model.GetEntityTypes().Select(e => e.ClrType.Name).ToList();

        entityTypes.Should().Contain("InboxState");
        entityTypes.Should().Contain("OutboxMessage");
        entityTypes.Should().Contain("OutboxState");
    }

    [Fact]
    public void AddShopSphereOutbox_RegistersOutboxServices()
    {
        var services = new ServiceCollection();
        services.AddMassTransit(x =>
        {
            x.AddShopSphereOutbox<TestAppDbContext>();
            x.UsingInMemory();
        });

        var provider = services.BuildServiceProvider();
        provider.Should().NotBeNull();
    }

    [Fact]
    public void AuditSaveChangesInterceptor_SetsTimestampsOnAddedAndModified()
    {
        var options = new DbContextOptionsBuilder<TestAppDbContext>()
            .UseSqlServer("Server=dummy;Database=dummy;TrustServerCertificate=True;")
            .Options;

        using var context = new TestAppDbContext(options);
        var entity = new TestAuditableEntity(Guid.NewGuid(), "Initial");

        context.AuditableEntities.Add(entity);
        entity.CreatedAtUtc.Should().Be(default);

        AuditSaveChangesInterceptor.UpdateAuditableEntities(context);

        entity.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        entity.UpdatedAtUtc.Should().BeNull();

        // Simulate modified
        context.Entry(entity).State = EntityState.Modified;
        AuditSaveChangesInterceptor.UpdateAuditableEntities(context);

        entity.UpdatedAtUtc.Should().NotBeNull();
        entity.UpdatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }
}
