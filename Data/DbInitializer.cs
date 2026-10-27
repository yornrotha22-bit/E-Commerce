using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RothaShop.Models;

namespace RothaShop.Data;

/// <summary>
/// Creates the four roles, the initial SuperAdmin, default store settings and demo catalog data.
/// The whole operation is idempotent: it can run on every application start.
/// </summary>
public class DbInitializer
{
    // Role names are defined once in Security/Roles.cs to avoid drift.
    public const string SuperAdminRole = Security.Roles.SuperAdmin;
    public const string AdminRole = Security.Roles.Admin;
    public const string StaffRole = Security.Roles.Staff;
    public const string CustomerRole = Security.Roles.Customer;

    public static readonly string[] Roles = Security.Roles.All;

    private readonly ApplicationDbContext _dbContext;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(
        ApplicationDbContext dbContext,
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger<DbInitializer> logger)
    {
        _dbContext = dbContext;
        _roleManager = roleManager;
        _userManager = userManager;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync();
        await SeedSuperAdminAsync();
        await SeedStoreSettingsAsync(cancellationToken);
        await SeedCatalogAsync(cancellationToken);
    }

    private async Task SeedRolesAsync()
    {
        foreach (var roleName in Roles)
        {
            if (await _roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await _roleManager.CreateAsync(new IdentityRole(roleName));

            if (result.Succeeded)
            {
                _logger.LogInformation("Identity role '{Role}' created.", roleName);
            }
            else
            {
                _logger.LogError(
                    "Could not create role '{Role}': {Errors}",
                    roleName,
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    private async Task SeedSuperAdminAsync()
    {
        var email = _configuration["SuperAdmin:Email"];
        var password = _configuration["SuperAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning(
                "SuperAdmin seeding skipped. Configure 'SuperAdmin:Email' and 'SuperAdmin:Password' " +
                "(environment variables or a secret store) to create the initial administrator.");
            return;
        }

        var adminUser = await _userManager.FindByEmailAsync(email);

        if (adminUser is null)
        {
            adminUser = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = "KShop Super Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(adminUser, password);

            if (!createResult.Succeeded)
            {
                _logger.LogError(
                    "Could not create the initial SuperAdmin '{Email}': {Errors}",
                    email,
                    string.Join("; ", createResult.Errors.Select(e => e.Description)));
                return;
            }

            _logger.LogInformation(
                "Initial SuperAdmin '{Email}' created. Change the password before going to production.",
                email);
        }

        if (!await _userManager.IsInRoleAsync(adminUser, SuperAdminRole))
        {
            var roleResult = await _userManager.AddToRoleAsync(adminUser, SuperAdminRole);

            if (!roleResult.Succeeded)
            {
                _logger.LogError(
                    "Could not assign the SuperAdmin role to '{Email}': {Errors}",
                    email,
                    string.Join("; ", roleResult.Errors.Select(e => e.Description)));
            }
        }
    }

    private async Task SeedStoreSettingsAsync(CancellationToken cancellationToken)
    {
        if (await _dbContext.StoreSettings.AnyAsync(cancellationToken))
        {
            return;
        }

        _dbContext.StoreSettings.AddRange(
            new StoreSetting
            {
                Key = "StoreName",
                Value = _configuration["Store:Name"] ?? "KShop",
                Description = "Display name of the store."
            },
            new StoreSetting
            {
                Key = "Currency",
                Value = _configuration["Store:Currency"] ?? "USD",
                Description = "Default store currency code."
            },
            new StoreSetting
            {
                Key = "DefaultShippingFee",
                Value = (_configuration.GetValue<decimal?>("Store:DefaultShippingFee") ?? 2.00m)
                    .ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                Description = "Flat shipping fee applied at checkout."
            },
            new StoreSetting
            {
                Key = "FreeShippingThreshold",
                Value = "50.00",
                Description = "Order subtotal from which shipping is free."
            },
            new StoreSetting
            {
                Key = "SupportEmail",
                Value = "support@kshop.local",
                Description = "Customer support email address."
            },
            new StoreSetting
            {
                Key = "SupportPhone",
                Value = "+855 00 000 000",
                Description = "Customer support phone number."
            });

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Default store settings created.");
    }

    private async Task SeedCatalogAsync(CancellationToken cancellationToken)
    {
        if (await _dbContext.Brands.AnyAsync(cancellationToken)
            || await _dbContext.Categories.AnyAsync(cancellationToken)
            || await _dbContext.Products.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Catalog seeding skipped because catalog data already exists.");
            return;
        }

        var brands = new Dictionary<string, Brand>(StringComparer.OrdinalIgnoreCase)
        {
            ["apple"] = new Brand { Name = "Apple", Slug = "apple", Description = "Smartphones and accessories from Apple." },
            ["samsung"] = new Brand { Name = "Samsung", Slug = "samsung", Description = "Smartphones and mobile accessories from Samsung." },
            ["xiaomi"] = new Brand { Name = "Xiaomi", Slug = "xiaomi", Description = "Smartphones and accessories from Xiaomi." },
            ["anker"] = new Brand { Name = "Anker", Slug = "anker", Description = "Chargers, cables and power banks." },
            ["jbl"] = new Brand { Name = "JBL", Slug = "jbl", Description = "Headphones and portable audio." },
            ["kshop"] = new Brand { Name = "KShop Essentials", Slug = "kshop-essentials", Description = "Cases, protectors and everyday accessories." }
        };

        var categories = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase)
        {
            ["smartphones"] = new Category { Name = "Smartphones", Slug = "smartphones", DisplayOrder = 1, Description = "Phones for every budget." },
            ["cases"] = new Category { Name = "Phone Cases", Slug = "phone-cases", DisplayOrder = 2, Description = "Protection for your device." },
            ["chargers"] = new Category { Name = "Chargers & Adapters", Slug = "chargers-adapters", DisplayOrder = 3, Description = "Fast chargers, cables and adapters." },
            ["audio"] = new Category { Name = "Earphones & Headphones", Slug = "earphones-headphones", DisplayOrder = 4, Description = "Wired and wireless audio." },
            ["power"] = new Category { Name = "Power Banks", Slug = "power-banks", DisplayOrder = 5, Description = "Portable power on the go." },
            ["protection"] = new Category { Name = "Screen Protectors", Slug = "screen-protectors", DisplayOrder = 6, Description = "Tempered glass and films." },
            ["other"] = new Category { Name = "Other Accessories", Slug = "other-accessories", DisplayOrder = 7, Description = "Everything else for your phone." }
        };

        _dbContext.Brands.AddRange(brands.Values);
        _dbContext.Categories.AddRange(categories.Values);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var products = new List<Product>
        {
            new()
            {
                Name = "iPhone 15",
                Slug = "iphone-15",
                Description = "A 6.1-inch smartphone with an advanced dual camera system, USB-C charging and all-day battery life.",
                Brand = brands["apple"],
                Category = categories["smartphones"],
                IsFeatured = true,
                Variants =
                {
                    new ProductVariant { SKU = "IPH15-128-BLK", Color = "Black", Storage = "128GB", Price = 799.00m, StockQuantity = 12 },
                    new ProductVariant { SKU = "IPH15-256-BLU", Color = "Blue", Storage = "256GB", Price = 899.00m, StockQuantity = 8 },
                    new ProductVariant { SKU = "IPH15-512-GRN", Color = "Green", Storage = "512GB", Price = 1099.00m, StockQuantity = 3, LowStockThreshold = 5 }
                }
            },
            new()
            {
                Name = "Galaxy S24",
                Slug = "galaxy-s24",
                Description = "A compact flagship smartphone with a bright display, triple camera and long software support.",
                Brand = brands["samsung"],
                Category = categories["smartphones"],
                IsFeatured = true,
                Variants =
                {
                    new ProductVariant { SKU = "S24-256-BLK", Color = "Phantom Black", Storage = "256GB", Price = 849.00m, StockQuantity = 10 },
                    new ProductVariant { SKU = "S24-512-VIO", Color = "Violet", Storage = "512GB", Price = 949.00m, StockQuantity = 5 }
                }
            },
            new()
            {
                Name = "Redmi Note 13",
                Slug = "redmi-note-13",
                Description = "A value-focused smartphone with a large AMOLED display, fast charging and a 108 MP main camera.",
                Brand = brands["xiaomi"],
                Category = categories["smartphones"],
                Variants =
                {
                    new ProductVariant { SKU = "RN13-128-BLK", Color = "Midnight Black", Storage = "128GB", Price = 199.00m, StockQuantity = 25 },
                    new ProductVariant { SKU = "RN13-256-BLU", Color = "Ocean Blue", Storage = "256GB", Price = 229.00m, StockQuantity = 18 }
                }
            },
            new()
            {
                Name = "Anker 20W USB-C Charger",
                Slug = "anker-20w-usb-c-charger",
                Description = "Compact wall charger with USB-C Power Delivery for fast charging supported phones.",
                Brand = brands["anker"],
                Category = categories["chargers"],
                IsFeatured = true,
                Variants =
                {
                    new ProductVariant { SKU = "ANK-CHG-20W-WHT", Color = "White", Price = 19.90m, StockQuantity = 40 }
                }
            },
            new()
            {
                Name = "Anker PowerCore 10000",
                Slug = "anker-powercore-10000",
                Description = "10,000 mAh power bank with fast charging output and a compact travel-friendly body.",
                Brand = brands["anker"],
                Category = categories["power"],
                Variants =
                {
                    new ProductVariant { SKU = "ANK-PB-10000-BLK", Color = "Black", Price = 29.90m, StockQuantity = 22 }
                }
            },
            new()
            {
                Name = "JBL Tune 510BT Headphones",
                Slug = "jbl-tune-510bt-headphones",
                Description = "Wireless on-ear headphones with a lightweight design and long battery life.",
                Brand = brands["jbl"],
                Category = categories["audio"],
                IsFeatured = true,
                Variants =
                {
                    new ProductVariant { SKU = "JBL-T510-BLK", Color = "Black", Price = 49.90m, CompareAtPrice = 59.90m, StockQuantity = 15 },
                    new ProductVariant { SKU = "JBL-T510-WHT", Color = "White", Price = 49.90m, CompareAtPrice = 59.90m, StockQuantity = 7 }
                }
            },
            new()
            {
                Name = "Clear Shockproof Phone Case",
                Slug = "clear-shockproof-phone-case",
                Description = "Transparent protective case with reinforced corners. Available for several popular phone models.",
                Brand = brands["kshop"],
                Category = categories["cases"],
                Variants =
                {
                    new ProductVariant { SKU = "RE-CASE-CLR-IPH15", Color = "Clear", Price = 9.90m, StockQuantity = 60 },
                    new ProductVariant { SKU = "RE-CASE-CLR-S24", Color = "Clear", Price = 9.90m, StockQuantity = 45 }
                }
            },
            new()
            {
                Name = "Tempered Glass Screen Protector",
                Slug = "tempered-glass-screen-protector",
                Description = "9H tempered glass with an oleophobic coating and easy installation kit.",
                Brand = brands["kshop"],
                Category = categories["protection"],
                Variants =
                {
                    new ProductVariant { SKU = "RE-GLASS-IPH15", Price = 5.90m, StockQuantity = 100 },
                    new ProductVariant { SKU = "RE-GLASS-S24", Price = 5.90m, StockQuantity = 80 }
                }
            }
        };

        _dbContext.Products.AddRange(products);

        _dbContext.Coupons.Add(new Coupon
        {
            Code = "WELCOME10",
            Description = "10% off orders from $50.",
            DiscountType = DiscountType.Percentage,
            DiscountValue = 10m,
            MinimumOrderAmount = 50m,
            MaxDiscountAmount = 20m,
            StartsAt = DateTime.UtcNow,
            EndsAt = DateTime.UtcNow.AddYears(1),
            UsageLimit = 1000,
            IsActive = true
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Demo catalog, default coupon and product variants created.");
    }

    /// <summary>
    /// Creates a URL friendly slug. Used when administrators do not supply one.
    /// </summary>
    public static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);

        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
            else if (ch is ' ' or '-' or '_' or '&' or '/' or '.')
            {
                builder.Append('-');
            }
        }

        return Regex.Replace(builder.ToString(), "-{2,}", "-").Trim('-');
    }
}
