using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ShopSphere.BuildingBlocks.Security;
using ShopSphere.Services.Identity.Application.Common.Interfaces;
using ShopSphere.Services.Identity.Domain;
using ShopSphere.Services.Identity.Infrastructure.Services;

namespace ShopSphere.Services.Identity.Infrastructure;

public static class IdentityDbSeeder
{
    public static readonly Guid AdminRoleId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid CustomerRoleId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid DefaultAdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public const string DefaultAdminEmail = "admin@shopsphere.local";
    public const string DefaultAdminPassword = "AdminPassword123!";
    public const string DefaultAdminFullName = "ShopSphere Administrator";

    private static readonly Dictionary<string, (Guid Id, string Description)> _corePermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        [ShopSpherePermissions.CatalogRead] = (Guid.Parse("10000000-0000-0000-0000-000000000001"), "Read catalog products"),
        [ShopSpherePermissions.CatalogCreate] = (Guid.Parse("10000000-0000-0000-0000-000000000002"), "Create catalog products"),
        [ShopSpherePermissions.CatalogUpdate] = (Guid.Parse("10000000-0000-0000-0000-000000000003"), "Update catalog products"),
        [ShopSpherePermissions.CatalogPriceUpdate] = (Guid.Parse("10000000-0000-0000-0000-000000000004"), "Update product prices"),
        [ShopSpherePermissions.InventoryRead] = (Guid.Parse("10000000-0000-0000-0000-000000000005"), "Read inventory stock levels"),
        [ShopSpherePermissions.InventoryAdjust] = (Guid.Parse("10000000-0000-0000-0000-000000000006"), "Adjust inventory stock"),
        [ShopSpherePermissions.CartManage] = (Guid.Parse("10000000-0000-0000-0000-000000000007"), "Manage shopping cart"),
        [ShopSpherePermissions.PaymentRead] = (Guid.Parse("10000000-0000-0000-0000-000000000008"), "Read payment transactions"),
        [ShopSpherePermissions.PaymentCreate] = (Guid.Parse("10000000-0000-0000-0000-000000000013"), "Create payment transactions"),
        [ShopSpherePermissions.PaymentRefund] = (Guid.Parse("10000000-0000-0000-0000-000000000009"), "Process payment refunds"),
        [ShopSpherePermissions.UsersRead] = (Guid.Parse("10000000-0000-0000-0000-000000000010"), "Read user accounts and roles"),
        [ShopSpherePermissions.UsersManage] = (Guid.Parse("10000000-0000-0000-0000-000000000011"), "Manage user accounts and roles"),
        [ShopSpherePermissions.SuperAdmin] = (Guid.Parse("10000000-0000-0000-0000-000000000012"), "Super administrator wildcard access")
    };

    public static Task SeedAsync(
        IdentityDbContext context,
        IPasswordHashService? passwordHashService = null,
        IConfiguration? configuration = null,
        CancellationToken ct = default)
    {
        return SeedAsync((IIdentityDbContext)context, passwordHashService, configuration, ct);
    }

    public static async Task SeedAsync(
        IIdentityDbContext context,
        IPasswordHashService? passwordHashService = null,
        IConfiguration? configuration = null,
        CancellationToken ct = default)
    {
        var hasher = passwordHashService ?? new PasswordHashService();
        var adminEmail = configuration?["Identity:DefaultAdmin:Email"] ?? DefaultAdminEmail;
        var adminPassword = configuration?["Identity:DefaultAdmin:Password"] ?? DefaultAdminPassword;

        // 1. Seed Permissions
        var existingPermissions = await context.Permissions.ToListAsync(ct);
        var permMap = existingPermissions.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);

        var permissionsChanged = false;
        foreach (var (code, (id, description)) in _corePermissions)
        {
            if (!permMap.TryGetValue(code, out var perm))
            {
                perm = Permission.Create(id, code, description);
                context.Permissions.Add(perm);
                permMap[code] = perm;
                permissionsChanged = true;
            }
        }

        if (permissionsChanged)
        {
            await context.SaveChangesAsync(ct);
        }

        // 2. Seed Roles
        var existingRoles = await context.Roles.ToListAsync(ct);
        var adminRole = existingRoles.FirstOrDefault(r => string.Equals(r.Name, "Admin", StringComparison.OrdinalIgnoreCase));
        if (adminRole is null)
        {
            adminRole = Role.Create(AdminRoleId, "Admin", "Administrator role with full system privileges");
            context.Roles.Add(adminRole);
            await context.SaveChangesAsync(ct);
        }

        var customerRole = existingRoles.FirstOrDefault(r => string.Equals(r.Name, "Customer", StringComparison.OrdinalIgnoreCase));
        if (customerRole is null)
        {
            customerRole = Role.Create(CustomerRoleId, "Customer", "Standard customer account");
            context.Roles.Add(customerRole);
            await context.SaveChangesAsync(ct);
        }

        // Assign permissions to Admin role
        var existingRolePermissions = await context.RolePermissions.ToListAsync(ct);
        var adminPermIds = new HashSet<Guid>(
            existingRolePermissions
                .Where(rp => rp.RoleId == adminRole.Id)
                .Select(rp => rp.PermissionId)
                .Concat(adminRole.RolePermissions.Select(rp => rp.PermissionId)));

        var adminPermsChanged = false;
        foreach (var perm in permMap.Values)
        {
            if (!adminPermIds.Contains(perm.Id))
            {
                adminRole.AddPermission(perm.Id);
                if (context is not DbContext)
                {
                    context.RolePermissions.Add(new RolePermission(adminRole.Id, perm.Id));
                }
                adminPermIds.Add(perm.Id);
                adminPermsChanged = true;
            }
        }

        // Assign permissions to Customer role (CartManage, CatalogRead)
        var customerPermCodes = new[] { ShopSpherePermissions.CartManage, ShopSpherePermissions.CatalogRead };
        var customerPermIds = new HashSet<Guid>(
            existingRolePermissions
                .Where(rp => rp.RoleId == customerRole.Id)
                .Select(rp => rp.PermissionId)
                .Concat(customerRole.RolePermissions.Select(rp => rp.PermissionId)));

        var customerPermsChanged = false;
        foreach (var code in customerPermCodes)
        {
            if (permMap.TryGetValue(code, out var perm) && !customerPermIds.Contains(perm.Id))
            {
                customerRole.AddPermission(perm.Id);
                if (context is not DbContext)
                {
                    context.RolePermissions.Add(new RolePermission(customerRole.Id, perm.Id));
                }
                customerPermIds.Add(perm.Id);
                customerPermsChanged = true;
            }
        }

        if (adminPermsChanged || customerPermsChanged)
        {
            await context.SaveChangesAsync(ct);
        }

        // 3. Seed Default Administrator
        var normalizedAdminEmail = adminEmail.Trim().ToLowerInvariant();
        var adminUser = await context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedAdminEmail, ct);

        if (adminUser is null)
        {
            var hash = hasher.HashPassword(adminPassword);
            adminUser = User.Create(DefaultAdminId, normalizedAdminEmail, hash, DefaultAdminFullName);
            adminUser.AssignRole(adminRole.Id);
            context.Users.Add(adminUser);
            if (context is not DbContext)
            {
                context.UserRoles.Add(new UserRole(adminUser.Id, adminRole.Id));
            }
            await context.SaveChangesAsync(ct);
        }
        else
        {
            var hasAdminRole = adminUser.UserRoles.Any(ur => ur.RoleId == adminRole.Id)
                || await context.UserRoles.AnyAsync(ur => ur.UserId == adminUser.Id && ur.RoleId == adminRole.Id, ct);

            if (!hasAdminRole)
            {
                adminUser.AssignRole(adminRole.Id);
                if (context is not DbContext)
                {
                    context.UserRoles.Add(new UserRole(adminUser.Id, adminRole.Id));
                }
                await context.SaveChangesAsync(ct);
            }
        }
    }
}
