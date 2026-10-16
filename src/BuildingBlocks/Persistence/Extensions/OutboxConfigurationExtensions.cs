using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace ShopSphere.BuildingBlocks.Persistence.Extensions;

public static class OutboxConfigurationExtensions
{
    public static IBusRegistrationConfigurator AddShopSphereOutbox<TDbContext>(
        this IBusRegistrationConfigurator configurator)
        where TDbContext : DbContext
    {
        configurator.AddEntityFrameworkOutbox<TDbContext>(o =>
        {
            o.UseSqlServer();
            o.UseBusOutbox();
        });

        return configurator;
    }

    public static ModelBuilder AddShopSphereOutboxEntities(this ModelBuilder modelBuilder)
    {
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
        return modelBuilder;
    }
}
