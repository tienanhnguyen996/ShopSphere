using ShopSphere.Services.Identity.Domain;

namespace ShopSphere.Services.Identity.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions);
}
