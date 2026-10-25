using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShopSphere.Services.Catalog.Application;
using ShopSphere.Services.Catalog.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var host = builder.Build();

try
{
    using var scope = host.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetService<CatalogDbContext>();
    if (dbContext is not null)
    {
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync(dbContext.Database);
        await CatalogDbSeeder.SeedAsync(dbContext);
    }
}
catch
{
    // Ignore concurrent migration race if API is migrating simultaneously
}

host.Run();
