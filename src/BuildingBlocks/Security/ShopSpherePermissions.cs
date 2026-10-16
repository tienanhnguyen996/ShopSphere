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
    public const string PaymentCreate = "payment:create";
    public const string PaymentRefund = "payment:refund";
    public const string UsersRead = "users:read";
    public const string UsersManage = "users:manage";
    public const string SuperAdmin = "*:*";

    public static class Catalog
    {
        public const string ReadProduct = CatalogRead;
        public const string CreateProduct = CatalogCreate;
        public const string UpdateProduct = CatalogUpdate;
        public const string UpdatePrice = CatalogPriceUpdate;
    }

    public static class Inventory
    {
        public const string ReadStock = InventoryRead;
        public const string AdjustStock = InventoryAdjust;
    }

    public static class Cart
    {
        public const string Manage = CartManage;
    }

    public static class Payment
    {
        public const string Read = PaymentRead;
        public const string Create = PaymentCreate;
        public const string Refund = PaymentRefund;
    }

    public static class Users
    {
        public const string Read = UsersRead;
        public const string Manage = UsersManage;
    }

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
        PaymentCreate,
        PaymentRefund,
        UsersRead,
        UsersManage,
        SuperAdmin
    };
}

