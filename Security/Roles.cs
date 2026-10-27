namespace RothaShop.Security;

/// <summary>
/// Canonical role names. Always use these constants instead of string literals.
/// </summary>
public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Staff = "Staff";
    public const string Customer = "Customer";

    public static readonly string[] All =
    {
        SuperAdmin,
        Admin,
        Staff,
        Customer
    };

    /// <summary>
    /// Roles allowed to manage products, brands and categories.
    /// </summary>
    public static readonly string[] CatalogManagers =
    {
        SuperAdmin,
        Admin
    };

    /// <summary>
    /// Roles allowed to open the admin area.
    /// </summary>
    public static readonly string[] StaffOrAbove =
    {
        SuperAdmin,
        Admin,
        Staff
    };
}

/// <summary>
/// Authorization policy names. Permissions are always enforced on the server.
/// </summary>
public static class Policies
{
    public const string AdminArea = "AdminArea";
    public const string ManageCatalog = "ManageCatalog";
    public const string ManagePromotions = "ManagePromotions";
    public const string ManageOrders = "ManageOrders";
    public const string ManageCustomers = "ManageCustomers";
    public const string ManagePayments = "ManagePayments";
    public const string ViewReports = "ViewReports";
    public const string ManageUsers = "ManageUsers";
}
