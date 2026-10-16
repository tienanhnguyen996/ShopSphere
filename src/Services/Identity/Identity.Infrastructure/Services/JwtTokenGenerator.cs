using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ShopSphere.BuildingBlocks.Security;
using ShopSphere.Services.Identity.Application.Common.Interfaces;
using ShopSphere.Services.Identity.Domain;

namespace ShopSphere.Services.Identity.Infrastructure.Services;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(User user, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(user);

        var secretKey = _configuration["Jwt:SecretKey"]
            ?? _configuration["Jwt:Key"]
            ?? AuthenticationExtensions.DefaultDevKey;

        var issuer = _configuration["Jwt:Issuer"] ?? AuthenticationExtensions.DefaultIssuer;
        var audience = _configuration["Jwt:Audience"] ?? AuthenticationExtensions.DefaultAudience;
        var expiryMinutes = _configuration.GetValue<int?>("Jwt:ExpiryMinutes") ?? 15;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (roles is not null)
        {
            foreach (var role in roles.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct())
            {
                claims.Add(new Claim("role", role));
            }
        }

        var expandedPermissions = ExpandPermissions(permissions);
        foreach (var perm in expandedPermissions)
        {
            claims.Add(new Claim("permission", perm));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static HashSet<string> ExpandPermissions(IReadOnlyCollection<string>? permissions)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (permissions is null)
        {
            return result;
        }

        foreach (var permission in permissions)
        {
            if (string.IsNullOrWhiteSpace(permission))
            {
                continue;
            }

            var trimmed = permission.Trim();
            if (trimmed == ShopSpherePermissions.SuperAdmin || trimmed == "*")
            {
                result.Add(ShopSpherePermissions.SuperAdmin);
                foreach (var p in ShopSpherePermissions.All)
                {
                    result.Add(p);
                }
            }
            else if (trimmed.EndsWith(":*"))
            {
                var prefix = trimmed[..^1]; // e.g. "catalog:"
                var matched = ShopSpherePermissions.All
                    .Where(p => p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matched.Count > 0)
                {
                    foreach (var m in matched)
                    {
                        result.Add(m);
                    }
                }
                else
                {
                    result.Add(trimmed);
                }
            }
            else
            {
                result.Add(trimmed);
            }
        }

        return result;
    }
}
