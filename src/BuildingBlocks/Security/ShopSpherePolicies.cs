namespace ShopSphere.BuildingBlocks.Security;

public static class ShopSpherePolicies
{
    public const string CanReadCatalog = ShopSpherePermissions.CatalogRead;
    public const string CanCreateProduct = ShopSpherePermissions.CatalogCreate;
    public const string CanUpdateProduct = ShopSpherePermissions.CatalogUpdate;
    public const string CanUpdatePrice = ShopSpherePermissions.CatalogPriceUpdate;

    public const string CanReadInventory = ShopSpherePermissions.InventoryRead;
    public const string CanAdjustStock = ShopSpherePermissions.InventoryAdjust;

    public const string CanManageCart = ShopSpherePermissions.CartManage;

    public const string CanReadPayment = ShopSpherePermissions.PaymentRead;
    public const string CanCreatePayment = ShopSpherePermissions.PaymentCreate;
    public const string CanRefundPayment = ShopSpherePermissions.PaymentRefund;

    public const string CanReadUsers = ShopSpherePermissions.UsersRead;
    public const string CanManageUsers = ShopSpherePermissions.UsersManage;
}
