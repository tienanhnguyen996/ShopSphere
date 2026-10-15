using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShopSphere.Services.Catalog.Application;
using ShopSphere.Services.Catalog.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var host = builder.Build();
host.Run();
