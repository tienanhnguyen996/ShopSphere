namespace ShopSphere.Services.Identity.Application.Users.Queries.GetUsers;

public sealed record GetUsersQuery(
    int Page = 1,
    int PageSize = 20,
    string? SearchTerm = null);
