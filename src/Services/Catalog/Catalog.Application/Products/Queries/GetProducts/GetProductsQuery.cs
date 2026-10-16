namespace ShopSphere.Services.Catalog.Application.Products.Queries.GetProducts;

public sealed record GetProductsQuery(
    int Page = 1,
    int PageSize = 20,
    bool OnlyActive = true);
