using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Identity.Application.Auth.Queries.GetCurrentUser;
using ShopSphere.Services.Identity.Application.Contracts;

namespace ShopSphere.Services.Identity.Application.Users.Queries.GetUserById;

public class GetUserByIdQueryHandler
{
    private readonly GetCurrentUserQueryHandler _getCurrentUserQueryHandler;

    public GetUserByIdQueryHandler(GetCurrentUserQueryHandler getCurrentUserQueryHandler)
    {
        _getCurrentUserQueryHandler = getCurrentUserQueryHandler;
    }

    public Task<Result<UserProfileDto>> HandleAsync(GetUserByIdQuery query, CancellationToken ct = default)
    {
        return _getCurrentUserQueryHandler.HandleAsync(new GetCurrentUserQuery(query.Id), ct);
    }
}
