using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using RothaShop.Data;
using RothaShop.Localization;
using RothaShop.Models;
using RothaShop.Security;
using RothaShop.Services;
using System.Globalization;

// Session key used to throttle the "is this account still allowed?" database check.
const string AuthStatusCheckedAtKey = "Auth.StatusCheckedAt";

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Database (SQL Server)
// ---------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found in configuration.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 5);
        npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
        // Avoid cartesian explosion when several collections are included (e.g. order details).
        npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
    }));

// ---------------------------------------------------------------------------
// ASP.NET Core Identity
// ---------------------------------------------------------------------------
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredUniqueChars = 4;

        options.User.RequireUniqueEmail = true;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;

        options.SignIn.RequireConfirmedAccount = false;
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication()
        .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
        {
            options.ClientId = googleClientId;
            options.ClientSecret = googleClientSecret;
        });
}

// Role changes bump the security stamp; validate it sooner than the 30 minute default
// so revoked privileges do not linger for long.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(5);
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "RothaShop.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;

    // A deactivated account (or a deleted one) must lose access even when it still
    // holds a valid auth cookie. The database check is throttled per session so that
    // it costs at most one query every two minutes instead of one per request.
    options.Events.OnValidatePrincipal = async context =>
    {
        if (context.Principal?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var session = context.HttpContext.Session;
        var now = DateTime.UtcNow;

        if (session is not null)
        {
            var lastCheckedRaw = session.GetString(AuthStatusCheckedAtKey);
            if (long.TryParse(lastCheckedRaw, out var ticks)
                && now - new DateTime(ticks, DateTimeKind.Utc) < TimeSpan.FromMinutes(2))
            {
                return;
            }

            session.SetString(AuthStatusCheckedAtKey, now.Ticks.ToString());
        }

        var userManager = context.HttpContext.RequestServices
            .GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.GetUserAsync(context.Principal!);

        if (user is null || !user.IsActive)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    };
});

// ---------------------------------------------------------------------------
// MVC and application services
// ---------------------------------------------------------------------------
builder.Services.AddControllersWithViews(options =>
{
    // Model binding and Identity produce English validation text; the filter
    // rewrites it into the active language before the view is rendered.
    options.Filters.Add<ModelStateTranslationFilter>();
});

// ---------------------------------------------------------------------------
// UI language (English / Khmer)
// The current culture comes from the RothaShop.Culture cookie and drives
// CultureInfo.CurrentUICulture, which the scoped translator reads.
// ---------------------------------------------------------------------------
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    // The language switch changes UI text only. Numbers, money and timestamps keep
    // English/invariant formatting so a price never renders as "$55,80" in Khmer.
    var english = new CultureInfo(SupportedCultures.English);
    var khmer = (CultureInfo)CultureInfo.GetCultureInfo(SupportedCultures.Khmer).Clone();
    khmer.NumberFormat = (NumberFormatInfo)english.NumberFormat.Clone();
    khmer.DateTimeFormat = (DateTimeFormatInfo)english.DateTimeFormat.Clone();

    var cultures = new[] { english, khmer };

    options.DefaultRequestCulture = new RequestCulture(SupportedCultures.English);
    options.SupportedCultures = cultures;
    options.SupportedUICultures = cultures;

    // Deterministic: browser Accept-Language must not override the saved choice.
    options.RequestCultureProviders.Clear();
    options.RequestCultureProviders.Add(new CookieRequestCultureProvider
    {
        CookieName = SupportedCultures.CookieName
    });
    options.RequestCultureProviders.Add(new QueryStringRequestCultureProvider());
});

builder.Services.AddSingleton<TranslationStore>();
builder.Services.AddScoped<ITranslator>(provider =>
{
    var store = provider.GetRequiredService<TranslationStore>();
    var current = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    return new Translator(store.Get(current), store.Get(SupportedCultures.English));
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.Name = "RothaShop.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddScoped<DbInitializer>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IPaymentProvider, CodPaymentProvider>();
builder.Services.AddScoped<IPaymentProvider, BakongPaymentProvider>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddHttpClient("Bakong", client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditService, AuditService>();

// Transactional email. When Email:Enabled is false (the default) messages are
// logged instead of sent, so no SMTP server is required to run the store.
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.AddScoped<IEmailSender, EmailSender>();

// Admin views live under /Views/Admin/... (physical folder structure), so the
// area view engine also searches that location in addition to /Areas/Admin/Views.
builder.Services.Configure<RazorViewEngineOptions>(options =>
{
    options.AreaViewLocationFormats.Insert(0, "/Views/{2}/{1}/{0}.cshtml");
    options.AreaViewLocationFormats.Insert(1, "/Views/{2}/Shared/{0}.cshtml");
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AdminArea, policy =>
        policy.RequireRole(Roles.StaffOrAbove));

    options.AddPolicy(Policies.ManageCatalog, policy =>
        policy.RequireRole(Roles.CatalogManagers));

    options.AddPolicy(Policies.ManagePromotions, policy =>
        policy.RequireRole(Roles.CatalogManagers));

    options.AddPolicy(Policies.ManageOrders, policy =>
        policy.RequireRole(Roles.StaffOrAbove));

    options.AddPolicy(Policies.ManageCustomers, policy =>
        policy.RequireRole(Roles.StaffOrAbove));

    options.AddPolicy(Policies.ManagePayments, policy =>
        policy.RequireRole(Roles.CatalogManagers));

    options.AddPolicy(Policies.ViewReports, policy =>
        policy.RequireRole(Roles.CatalogManagers));

    // Only a SuperAdmin may assign or manage privileged accounts.
    options.AddPolicy(Policies.ManageUsers, policy =>
        policy.RequireRole(Roles.SuperAdmin));
});

var app = builder.Build();

if (app.Environment.IsProduction()
    && app.Configuration.GetValue<bool>("Seed:Enabled"))
{
    throw new InvalidOperationException(
        "Database seeding is enabled in Production. Disable Seed:Enabled and provision initial data through a controlled deployment step.");
}

// ---------------------------------------------------------------------------
// HTTP pipeline
// ---------------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}");
app.UseStaticFiles();
app.UseSession();
app.UseRequestLocalization();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ---------------------------------------------------------------------------
// Startup database work (opt-in through configuration)
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
    {
        try
        {
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Applying database migrations failed.");
            if (app.Environment.IsProduction())
            {
                throw;
            }
        }
    }

    if (app.Configuration.GetValue<bool>("Seed:Enabled"))
    {
        try
        {
            var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
            await initializer.InitializeAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database seeding failed. The application will continue to start.");
        }
    }
}

app.Run();

/// <summary>
/// Entry point marker used by integration tests (WebApplicationFactory).
/// </summary>
public partial class Program;
