using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RothaShop.Data;
using RothaShop.Localization;
using RothaShop.Models;
using RothaShop.Security;
using RothaShop.Services;
using RothaShop.ViewModels;
using System.Security.Claims;

namespace RothaShop.Controllers;

[Authorize]
public class AccountController : Controller
{
    private const int MaxAddressesPerCustomer = 10;
    private const string PasswordResetThrottleKey = "Account.PasswordResetRequestedAt";
    private static readonly TimeSpan PasswordResetCooldown = TimeSpan.FromSeconds(60);

    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly ICartService _cartService;
    private readonly IImageService _imageService;
    private readonly IEmailSender _emailSender;
    private readonly IAuditService _auditService;
    private readonly IConfiguration _configuration;
    private readonly ITranslator _t;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ICartService cartService,
        IImageService imageService,
        IEmailSender emailSender,
        IAuditService auditService,
        IConfiguration configuration,
        ITranslator translator,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _dbContext = dbContext;
        _cartService = cartService;
        _imageService = imageService;
        _emailSender = emailSender;
        _auditService = auditService;
        _configuration = configuration;
        _t = translator;
        _logger = logger;
    }

    // ---------------------------------------------------------------------
    // Authentication
    // ---------------------------------------------------------------------

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocalOrHome(returnUrl, User.IsInRole(Roles.Customer));
        }

        ViewData["ReturnUrl"] = returnUrl;
        ViewData["GoogleLoginEnabled"] = GoogleLoginIsConfigured();
        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["GoogleLoginEnabled"] = GoogleLoginIsConfigured();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);

        // Same message for unknown user, inactive user and wrong password.
        if (user is null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            user.LastLoginAt = DateTime.UtcNow;
            user.LastLoginDevice = GetLoginDevice();
            await _userManager.UpdateAsync(user);

            _logger.LogInformation("User {UserId} signed in.", user.Id);

            // Move a guest cart into the customer's account.
            await _cartService.MergeGuestCartAsync(user.Id);

            var isCustomer = await _userManager.IsInRoleAsync(user, Roles.Customer);
            return RedirectToLocalOrHome(returnUrl, isCustomer);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "This account is temporarily locked. Try again later.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(model);
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        if (!string.Equals(provider, "Google", StringComparison.Ordinal)
            || !GoogleLoginIsConfigured())
        {
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var callbackUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, callbackUrl!);
        return Challenge(properties, provider);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null)
    {
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            TempData["AccountError"] = _t["account.googleSignInFailed"];
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var signInResult = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider,
            info.ProviderKey,
            isPersistent: false,
            bypassTwoFactor: false);

        if (signInResult.Succeeded)
        {
            var signedInUser = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (signedInUser is null || !signedInUser.IsActive)
            {
                await _signInManager.SignOutAsync();
                TempData["AccountError"] = _t["account.googleSignInFailed"];
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            signedInUser.LastLoginAt = DateTime.UtcNow;
            signedInUser.LastLoginDevice = GetLoginDevice();
            await _userManager.UpdateAsync(signedInUser);
            await _cartService.MergeGuestCartAsync(signedInUser.Id);
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            var isCustomer = await _userManager.IsInRoleAsync(signedInUser, Roles.Customer);
            return RedirectToLocalOrHome(returnUrl, isCustomer);
        }

        if (signInResult.IsLockedOut || signInResult.IsNotAllowed || signInResult.RequiresTwoFactor)
        {
            TempData["AccountError"] = _t["account.googleSignInFailed"];
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["AccountError"] = _t["account.googleSignInFailed"];
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            TempData["AccountError"] = _t["account.googleAccountExists"];
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var fullName = info.Principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(fullName))
        {
            fullName = email[..email.IndexOf('@')];
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName.Length <= 150 ? fullName : fullName[..150],
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            LastLoginDevice = GetLoginDevice()
        };

        var createResult = await _userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            _logger.LogWarning("Google sign-in could not create an account for the returned email.");
            TempData["AccountError"] = _t["account.googleSignInFailed"];
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var roleResult = await _userManager.AddToRoleAsync(user, Roles.Customer);
        var loginResult = roleResult.Succeeded
            ? await _userManager.AddLoginAsync(user, info)
            : IdentityResult.Failed(roleResult.Errors.ToArray());

        if (!roleResult.Succeeded || !loginResult.Succeeded)
        {
            _logger.LogError(
                "Google sign-in setup failed for new customer {UserId}: {Errors}",
                user.Id,
                string.Join("; ", (roleResult.Errors.Concat(loginResult.Errors)).Select(error => error.Description)));
            await _userManager.DeleteAsync(user);
            TempData["AccountError"] = _t["account.googleSignInFailed"];
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        user.LastLoginAt = DateTime.UtcNow;
        user.LastLoginDevice = GetLoginDevice();
        await _userManager.UpdateAsync(user);
        await _signInManager.SignInAsync(user, isPersistent: false);
        await _cartService.MergeGuestCartAsync(user.Id);
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        TempData["AccountSuccess"] = _t["msg.welcomeAccount"];
        return RedirectToLocalOrHome(returnUrl, isCustomer: true);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Profile));
        }

        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Never reveal whether the address belongs to an account: the response is
        // always the same confirmation page. The work below is also throttled to
        // stop someone from flooding a mailbox through this form.
        var lastRequested = HttpContext.Session.GetString(PasswordResetThrottleKey);
        if (long.TryParse(lastRequested, out var ticks)
            && DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc) < PasswordResetCooldown)
        {
            _logger.LogInformation("Password reset request throttled.");
            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        HttpContext.Session.SetString(PasswordResetThrottleKey, DateTime.UtcNow.Ticks.ToString());

        var email = model.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email);

        if (user is not null && user.IsActive && !string.IsNullOrWhiteSpace(user.Email))
        {
            try
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var link = Url.Action(
                    nameof(ResetPassword),
                    "Account",
                    new { email = user.Email, token },
                    protocol: Request.Scheme);

                if (!string.IsNullOrWhiteSpace(link))
                {
                    var content = EmailTemplates.PasswordReset(
                        _configuration["Store:Name"] ?? "KStore",
                        user.FullName,
                        link);

                    await _emailSender.SendAsync(
                        user.Email,
                        content.Subject,
                        content.HtmlBody,
                        content.TextBody,
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                // A mail outage must not tell the caller whether the account exists.
                _logger.LogError(ex, "Password reset email failed for user {UserId}.", user.Id);
            }
        }

        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPasswordConfirmation() => View();

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetPassword(string? email, string? token)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new ResetPasswordViewModel
        {
            Email = email,
            Token = token
        });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email.Trim());

        if (user is null || !user.IsActive)
        {
            ModelState.AddModelError(
                string.Empty,
                "This reset link is invalid or has expired. Request a new one.");
            return View(model);
        }

        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                var message = error.Code == "InvalidToken"
                    ? "This reset link is invalid or has expired. Request a new one."
                    : error.Description;

                ModelState.AddModelError(string.Empty, message);
            }

            return View(model);
        }

        // ResetPasswordAsync already rotates the security stamp, which invalidates
        // existing cookies on their next validation.
        await _auditService.LogAsync(
            "PasswordResetCompleted",
            nameof(ApplicationUser),
            user.Id,
            $"Password reset completed through the email link for '{user.Email}'.",
            cancellationToken);

        _logger.LogInformation("Password reset completed for user {UserId}.", user.Id);

        TempData["AccountSuccess"] = _t["msg.passwordResetDone"];
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Profile));
        }

        ViewData["ReturnUrl"] = returnUrl;
        ViewData["GoogleLoginEnabled"] = GoogleLoginIsConfigured();
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["GoogleLoginEnabled"] = GoogleLoginIsConfigured();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = model.Email.Trim();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            // No email sender is configured yet. The account is usable immediately;
            // enable RequireConfirmedAccount and an email provider before production.
            EmailConfirmed = true,
            FullName = model.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            LastLoginDevice = GetLoginDevice()
        };

        var createResult = await _userManager.CreateAsync(user, model.Password);

        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                var message = error.Code is "DuplicateUserName" or "DuplicateEmail"
                    ? "An account with this email already exists."
                    : error.Description;

                ModelState.AddModelError(string.Empty, message);
            }

            return View(model);
        }

        // Public registration may only ever assign the Customer role.
        var roleResult = await _userManager.AddToRoleAsync(user, Roles.Customer);

        if (!roleResult.Succeeded)
        {
            _logger.LogError(
                "Customer role assignment failed for {UserId}: {Errors}",
                user.Id,
                string.Join("; ", roleResult.Errors.Select(e => e.Description)));

            // Do not leave an account without a role behind.
            await _userManager.DeleteAsync(user);

            ModelState.AddModelError(
                string.Empty,
                "The account could not be created because the customer role is unavailable. " +
                "Contact the store administrator.");

            return View(model);
        }

        await _signInManager.SignInAsync(user, isPersistent: false);

        // Move a guest cart into the new account.
        await _cartService.MergeGuestCartAsync(user.Id);

        _logger.LogInformation("Customer account {UserId} registered.", user.Id);
        TempData["AccountSuccess"] = _t["msg.welcomeAccount"];

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    // ---------------------------------------------------------------------
    // Customer dashboard
    // ---------------------------------------------------------------------

    /// <summary>
    /// Customer home for the account area. It reuses the same order, wishlist,
    /// address and cart sources as the dedicated pages so the numbers always match.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Challenge();
        }

        var orders = _dbContext.Orders.AsNoTracking().Where(o => o.UserId == userId);

        var orderCount = await orders.CountAsync(cancellationToken);

        var openOrderCount = await orders.CountAsync(
            o => o.OrderStatus == OrderStatus.Pending
                 || o.OrderStatus == OrderStatus.Confirmed
                 || o.OrderStatus == OrderStatus.Processing
                 || o.OrderStatus == OrderStatus.Shipped,
            cancellationToken);

        var totalSpent = await orders
            .Where(o => o.PaymentStatus == PaymentStatus.Paid)
            .SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;

        var recentOrders = await ProjectOrderRows(
                orders.OrderByDescending(o => o.CreatedAt).Take(4))
            .ToListAsync(cancellationToken);

        var actionNeededOrder = await ProjectOrderRows(
                orders.Where(o => o.OrderStatus != OrderStatus.Cancelled
                                  && (o.OrderStatus == OrderStatus.Pending
                                      || (o.PaymentMethod == PaymentMethod.OnlinePayment
                                          && o.PaymentStatus == PaymentStatus.Pending)))
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(1))
            .FirstOrDefaultAsync(cancellationToken);

        var wishlist = _dbContext.WishlistItems.AsNoTracking().Where(w => w.UserId == userId);

        var wishlistCount = await wishlist.CountAsync(cancellationToken);

        var wishlistPreview = await wishlist
            .OrderByDescending(w => w.CreatedAt)
            .Take(3)
            .Select(w => new WishlistRowViewModel
            {
                ProductId = w.ProductId,
                Name = w.Product!.Name,
                BrandName = w.Product.Brand != null ? w.Product.Brand.Name : string.Empty,
                ImageUrl = w.Product.MainImageUrl
                    ?? w.Product.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault(),
                MinPrice = w.Product.Variants
                    .Where(v => v.IsActive)
                    .Select(v => (decimal?)v.Price)
                    .Min() ?? 0m,
                InStock = w.Product.Variants.Any(v => v.IsActive && v.StockQuantity > 0),
                CreatedAt = w.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var addresses = await _dbContext.Addresses.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        var cart = await _cartService.GetCartAsync(cancellationToken);

        ViewData["Title"] = _t["account.dashboardTitle"];
        ViewData["AccountTab"] = "Dashboard";

        return View(new CustomerDashboardViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            MemberSince = user.CreatedAt,
            OrderCount = orderCount,
            OpenOrderCount = openOrderCount,
            TotalSpent = totalSpent,
            WishlistCount = wishlistCount,
            WishlistPreview = wishlistPreview,
            AddressCount = addresses.Count,
            DefaultAddress = addresses.FirstOrDefault(),
            CartItemCount = cart.ItemCount,
            RecentOrders = recentOrders,
            ActionNeededOrder = actionNeededOrder
        });
    }

    private static IQueryable<OrderListItemViewModel> ProjectOrderRows(IQueryable<Order> orders) =>
        orders.Select(o => new OrderListItemViewModel
        {
            Id = o.Id,
            OrderNumber = o.OrderNumber,
            CreatedAt = o.CreatedAt,
            OrderStatus = o.OrderStatus,
            PaymentStatus = o.PaymentStatus,
            PaymentMethod = o.PaymentMethod,
            TotalAmount = o.TotalAmount,
            ItemCount = o.Items.Sum(i => i.Quantity)
        });

    // ---------------------------------------------------------------------
    // Profile
    // ---------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Challenge();
        }

        ViewData["Title"] = "My profile";
        ViewData["AccountTab"] = "Profile";

        return View(new ProfileViewModel
        {
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            Email = user.Email ?? string.Empty,
            MemberSince = user.CreatedAt,
            AvatarUrl = user.AvatarUrl
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "My profile";
        ViewData["AccountTab"] = "Profile";

        var user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Challenge();
        }

        // The email is read-only here; it is never taken from the posted form.
        model.Email = user.Email ?? string.Empty;
        model.MemberSince = user.CreatedAt;
        model.AvatarUrl = user.AvatarUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.Avatar is { Length: > 0 })
        {
            var newAvatarUrl = await _imageService.SaveAvatarImageAsync(model.Avatar, cancellationToken);

            if (newAvatarUrl is null)
            {
                ModelState.AddModelError(
                    nameof(model.Avatar),
                    _t["msg.avatarInvalid"]);
                return View(model);
            }

            // Replacing a photo should not leave the old file behind.
            _imageService.TryDeleteAvatar(user.AvatarUrl);
            user.AvatarUrl = newAvatarUrl;
            model.AvatarUrl = newAvatarUrl;
        }
        else if (model.RemoveAvatar)
        {
            _imageService.TryDeleteAvatar(user.AvatarUrl);
            user.AvatarUrl = null;
            model.AvatarUrl = null;
        }

        user.FullName = model.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        TempData["AccountSuccess"] = _t["msg.profileUpdated"];
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet]
    public IActionResult ChangePassword()
    {
        ViewData["Title"] = "Change password";
        ViewData["AccountTab"] = "Password";
        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        ViewData["Title"] = "Change password";
        ViewData["AccountTab"] = "Password";

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Challenge();
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["AccountSuccess"] = _t["msg.passwordChanged"];
        return RedirectToAction(nameof(Profile));
    }

    // ---------------------------------------------------------------------
    // Addresses (always scoped to the signed-in customer)
    // ---------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Addresses(CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);

        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var addresses = await _dbContext.Addresses.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.Id)
            .ToListAsync(cancellationToken);

        ViewData["Title"] = "My addresses";
        ViewData["AccountTab"] = "Addresses";

        return View(addresses);
    }

    [HttpGet]
    public async Task<IActionResult> CreateAddress()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Challenge();
        }

        ViewData["Title"] = "Add address";
        ViewData["AccountTab"] = "Addresses";

        return View("AddressForm", new AddressFormViewModel
        {
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber ?? string.Empty,
            Country = "Cambodia"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAddress(
        AddressFormViewModel model,
        CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Add address";
        ViewData["AccountTab"] = "Addresses";

        var userId = _userManager.GetUserId(User);

        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            return View("AddressForm", model);
        }

        var addressCount = await _dbContext.Addresses
            .CountAsync(a => a.UserId == userId, cancellationToken);

        if (addressCount >= MaxAddressesPerCustomer)
        {
            ModelState.AddModelError(
                string.Empty,
                _t.Format("msg.maxAddresses", MaxAddressesPerCustomer));
            return View("AddressForm", model);
        }

        var address = new Address
        {
            UserId = userId,
            FullName = model.FullName.Trim(),
            PhoneNumber = model.PhoneNumber.Trim(),
            AddressLine1 = model.AddressLine1.Trim(),
            AddressLine2 = string.IsNullOrWhiteSpace(model.AddressLine2) ? null : model.AddressLine2.Trim(),
            City = model.City.Trim(),
            State = string.IsNullOrWhiteSpace(model.State) ? null : model.State.Trim(),
            PostalCode = string.IsNullOrWhiteSpace(model.PostalCode) ? null : model.PostalCode.Trim(),
            Country = model.Country.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        if (model.IsDefault || addressCount == 0)
        {
            await ClearDefaultFlagsAsync(userId, null, cancellationToken);
            address.IsDefault = true;
        }

        _dbContext.Addresses.Add(address);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["AccountSuccess"] = _t["msg.addressSaved"];
        return RedirectToAction(nameof(Addresses));
    }

    [HttpGet]
    public async Task<IActionResult> EditAddress(int id, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);

        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        // Ownership is part of the query: another customer's address returns 404.
        var address = await _dbContext.Addresses.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, cancellationToken);

        if (address is null)
        {
            return NotFound();
        }

        ViewData["Title"] = "Edit address";
        ViewData["AccountTab"] = "Addresses";

        return View("AddressForm", new AddressFormViewModel
        {
            Id = address.Id,
            FullName = address.FullName,
            PhoneNumber = address.PhoneNumber,
            AddressLine1 = address.AddressLine1,
            AddressLine2 = address.AddressLine2,
            City = address.City,
            State = address.State,
            PostalCode = address.PostalCode,
            Country = address.Country,
            IsDefault = address.IsDefault
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAddress(
        int id,
        AddressFormViewModel model,
        CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Edit address";
        ViewData["AccountTab"] = "Addresses";

        if (id != model.Id)
        {
            return BadRequest();
        }

        var userId = _userManager.GetUserId(User);

        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var address = await _dbContext.Addresses
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, cancellationToken);

        if (address is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View("AddressForm", model);
        }

        address.FullName = model.FullName.Trim();
        address.PhoneNumber = model.PhoneNumber.Trim();
        address.AddressLine1 = model.AddressLine1.Trim();
        address.AddressLine2 = string.IsNullOrWhiteSpace(model.AddressLine2) ? null : model.AddressLine2.Trim();
        address.City = model.City.Trim();
        address.State = string.IsNullOrWhiteSpace(model.State) ? null : model.State.Trim();
        address.PostalCode = string.IsNullOrWhiteSpace(model.PostalCode) ? null : model.PostalCode.Trim();
        address.Country = model.Country.Trim();
        address.UpdatedAt = DateTime.UtcNow;

        if (model.IsDefault)
        {
            await ClearDefaultFlagsAsync(userId, address.Id, cancellationToken);
            address.IsDefault = true;
        }
        else
        {
            var anotherDefaultExists = await _dbContext.Addresses
                .AnyAsync(a => a.UserId == userId && a.Id != address.Id && a.IsDefault, cancellationToken);

            // A customer always keeps exactly one default address.
            address.IsDefault = !anotherDefaultExists;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["AccountSuccess"] = _t["msg.addressUpdated"];
        return RedirectToAction(nameof(Addresses));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAddress(int id, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);

        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var address = await _dbContext.Addresses
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, cancellationToken);

        if (address is null)
        {
            return NotFound();
        }

        var wasDefault = address.IsDefault;

        _dbContext.Addresses.Remove(address);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (wasDefault)
        {
            var next = await _dbContext.Addresses
                .Where(a => a.UserId == userId)
                .OrderBy(a => a.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (next is not null)
            {
                next.IsDefault = true;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        TempData["AccountSuccess"] = _t["msg.addressRemoved"];
        return RedirectToAction(nameof(Addresses));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefaultAddress(int id, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);

        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var address = await _dbContext.Addresses
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, cancellationToken);

        if (address is null)
        {
            return NotFound();
        }

        await ClearDefaultFlagsAsync(userId, address.Id, cancellationToken);
        address.IsDefault = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["AccountSuccess"] = _t["msg.defaultAddressUpdated"];
        return RedirectToAction(nameof(Addresses));
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private async Task ClearDefaultFlagsAsync(
        string userId,
        int? exceptAddressId,
        CancellationToken cancellationToken)
    {
        var addresses = await _dbContext.Addresses
            .Where(a => a.UserId == userId && a.IsDefault)
            .ToListAsync(cancellationToken);

        foreach (var address in addresses)
        {
            if (exceptAddressId.HasValue && address.Id == exceptAddressId.Value)
            {
                continue;
            }

            address.IsDefault = false;
        }
    }

    private IActionResult RedirectToLocalOrHome(string? returnUrl, bool isCustomer)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return isCustomer
            ? RedirectToAction("Index", "Home")
            : RedirectToAction("Index", "Dashboard", new { area = "Admin" });
    }

    private bool GoogleLoginIsConfigured() =>
        !string.IsNullOrWhiteSpace(_configuration["Authentication:Google:ClientId"])
        && !string.IsNullOrWhiteSpace(_configuration["Authentication:Google:ClientSecret"]);

    private string? GetLoginDevice()
    {
        var userAgent = Request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(userAgent)) return null;

        string device;
        if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase)) device = "iPhone";
        else if (userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase)) device = "iPad";
        else if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
        {
            var model = System.Text.RegularExpressions.Regex.Match(userAgent, @";\s*([^;()]+?)\s+Build/");
            device = model.Success ? $"Android {model.Groups[1].Value.Trim()}" : "Android device";
        }
        else if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase)) device = "Windows computer";
        else if (userAgent.Contains("Macintosh", StringComparison.OrdinalIgnoreCase)) device = "Mac computer";
        else if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase)) device = "Linux computer";
        else device = "Unknown device";

        var browser = userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase) ? "Edge"
            : userAgent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase) ? "Firefox"
            : userAgent.Contains("CriOS/", StringComparison.OrdinalIgnoreCase) ? "Chrome on iOS"
            : userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase) ? "Chrome"
            : userAgent.Contains("Safari/", StringComparison.OrdinalIgnoreCase) ? "Safari"
            : "Browser";

        return $"{device} · {browser}";
    }
}
