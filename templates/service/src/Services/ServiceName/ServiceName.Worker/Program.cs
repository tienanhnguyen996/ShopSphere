using ShopSphere.Services.ServiceName.Application;
using ShopSphere.Services.ServiceName.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var host = builder.Build();
host.Run();
