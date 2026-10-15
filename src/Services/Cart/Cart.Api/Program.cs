var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "ShopSphere Cart API");

app.Run();

public partial class Program { }
