namespace ShopSphere.BuildingBlocks.Security;

public static class ShopSpherePermissions
{
    public const string CatalogRead = "catalog:products:read";
    public const string CatalogCreate = "catalog:products:create";
    public const string CatalogUpdate = "catalog:products:update";
    public const string CatalogPriceUpdate = "catalog:prices:update";
    public const string InventoryRead = "inventory:stock:read";
    public const string InventoryAdjust = "inventory:stock:adjust";
    public const string CartManage = "cart:manage";
    public const string PaymentRead = "payment:read";
    public const string PaymentRefund = "payment:refund";
    public const string UsersRead = "users:read";
    public const string UsersManage = "users:manage";
    public const string SuperAdmin = "*:*";

    public static readonly IReadOnlyCollection<string> All = new[]
    {
        CatalogRead,
        CatalogCreate,
        CatalogUpdate,
        CatalogPriceUpdate,
        InventoryRead,
        InventoryAdjust,
        CartManage,
        PaymentRead,
        PaymentRefund,
        UsersRead,
        UsersManage,
        SuperAdmin
    };
}
