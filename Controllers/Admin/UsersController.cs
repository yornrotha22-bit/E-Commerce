using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RothaShop.Data;
using RothaShop.Localization;
using RothaShop.Models;
using RothaShop.Security;
using RothaShop.Services;
using RothaShop.ViewModels.Admin;

namespace RothaShop.Controllers.Admin;

/// <summary>
/// Staff/administrator account management. Restricted to SuperAdmin accounts only.
/// Customers are managed separately in <see cref="CustomersController"/>.
/// </summary>
[Area("Admin")]
[Authorize(Policy = Policies.ManageUsers)]
public class UsersController : Controller
{
    private const int PageSize = 20;

    /// <summary>
    /// Roles that may be handed out from this screen. Customer accounts are created
    /// through public registration and are managed on the Customers page.
    /// </summary>
    private static readonly string[] AssignableRoles =
    {
        Roles.SuperAdmin,
        Roles.Admin,
        Roles.Staff
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly ITranslator _t;

    public UsersController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        ITranslator translator)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _auditService = auditService;
        _t = translator;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? role,
        int page = 1,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        var effectivePageSize = AdminPageSize.Normalize(pageSize, PageSize);

        var model = new AdminUserListViewModel
        {
            Search = search,
            Role = role,
            Page = page,
            PageSize = effectivePageSize,
            AvailableRoles = Roles.All
        };

        var query = _dbContext.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            
var pattern = SearchPattern.Contains(term);
            query = query.Where(u =>
                EF.Functions.ILike(u.FullName, pattern)
                || (u.Email != null && EF.Functions.ILike(u.Email, pattern))
                || (u.PhoneNumber != null && EF.Functions.ILike(u.PhoneNumber, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            if (!Roles.All.Contains(role, StringComparer.Ordinal))
            {
                model.Role = null;
            }
            else
            {
                var roleId = await _dbContext.Roles
                    .Where(r => r.Name == role)
                    .Select(r => r.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                if (roleId is null)
                {
                    query = query.Where(_ => false);
                }
                else
                {
                    query = query.Where(u =>
                        _dbContext.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == roleId));
                }
            }
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)effectivePageSize);
        page = Math.Min(page, totalPages);

        var now = DateTimeOffset.UtcNow;

        model.Page = page;
        model.TotalItems = totalItems;
        model.Items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(u => new AdminUserRow
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? string.Empty,
                PhoneNumber = u.PhoneNumber,
                IsActive = u.IsActive,
                IsLockedOut = u.LockoutEnd != null && u.LockoutEnd > now,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt
            })
            .ToListAsync(cancellationToken);

        var ids = model.Items.Select(u => u.Id).ToList();
        var roleLookup = await (
                from userRole in _dbContext.UserRoles.AsNoTracking()
                join r in _dbContext.Roles.AsNoTracking() on userRole.RoleId equals r.Id
                where ids.Contains(userRole.UserId)
                select new { userRole.UserId, RoleName = r.Name })
            .ToListAsync(cancellationToken);

        foreach (var row in model.Items)
        {
            row.Roles = roleLookup
                .Where(x => x.UserId == row.Id && x.RoleName != null)
                .Select(x => x.RoleName!)
                .OrderBy(name => Array.IndexOf(Roles.All, name))
                .ToList();
        }

        ViewData["Title"] = _t["admin.nav.users"];
        ViewData["Active"] = "Users";

        return View(model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = _t["adm.addTeamMember"];
        ViewData["Active"] = "Users";

        return View(new TeamMemberCreateViewModel
        {
            Role = Roles.Staff
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        TeamMemberCreateViewModel model,
        CancellationToken cancellationToken)
    {
        if (!AssignableRoles.Contains(model.Role, StringComparer.Ordinal))
        {
            ModelState.AddModelError(nameof(model.Role), "Choose a valid role.");
        }

        var email = model.Email.Trim();

        if (ModelState.IsValid && await _userManager.FindByEmailAsync(email) is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Add team member";
            ViewData["Active"] = "Users";
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = model.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, model.Password);

        if (!createResult.Succeeded)
        {
            AddIdentityErrorsToModelState(createResult);
            ViewData["Title"] = "Add team member";
            ViewData["Active"] = "Users";
            return View(model);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, model.Role);

        if (!roleResult.Succeeded)
        {
            // Do not leave an account without a role behind.
            await _userManager.DeleteAsync(user);
            AddIdentityErrorsToModelState(roleResult);
            ViewData["Title"] = "Add team member";
            ViewData["Active"] = "Users";
            return View(model);
        }

        await _auditService.LogAsync(
            "UserCreated",
            nameof(ApplicationUser),
            user.Id,
            $"Created {model.Role} account '{user.Email}'.",
            cancellationToken);

        TempData["Success"] = _t.Format("msg.teamAccountCreated", model.Role, user.Email);

        return RedirectToAction(nameof(Details), new { id = user.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id);

        if (user is null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);

        ViewData["Title"] = _t["adm.userDetails"];
        ViewData["Active"] = "Users";

        return View(new AdminUserDetailsViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            EmailConfirmed = user.EmailConfirmed,
            IsLockedOut = await _userManager.IsLockedOutAsync(user),
            LockoutEnd = user.LockoutEnd,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            OrderCount = await _dbContext.Orders
                .CountAsync(o => o.UserId == user.Id, cancellationToken),
            Roles = roles.OrderBy(name => Array.IndexOf(Roles.All, name)).ToList(),
            AllRoles = AssignableRoles,
            ResetPassword = new ResetUserPasswordViewModel
            {
                UserId = user.Id,
                FullName = user.FullName
            }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRoles(
        string id,
        string[]? roles,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id);

        if (user is null)
        {
            return NotFound();
        }

        var selected = (roles ?? Array.Empty<string>())
            .Where(r => AssignableRoles.Contains(r, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (selected.Count == 0)
        {
            TempData["Error"] = _t["msg.roleRequired"];
            return RedirectToAction(nameof(Details), new { id });
        }

        var currentUserId = _userManager.GetUserId(User);

        if (string.Equals(currentUserId, user.Id, StringComparison.Ordinal)
            && !selected.Contains(Roles.SuperAdmin, StringComparer.Ordinal))
        {
            TempData["Error"] = _t["msg.cannotRemoveOwnSuperAdmin"];
            return RedirectToAction(nameof(Details), new { id });
        }

        var currentRoles = await _userManager.GetRolesAsync(user);

        var losingSuperAdmin = currentRoles.Contains(Roles.SuperAdmin, StringComparer.Ordinal)
            && !selected.Contains(Roles.SuperAdmin, StringComparer.Ordinal);

        if (losingSuperAdmin && !await AnotherActiveSuperAdminExistsAsync(user.Id, cancellationToken))
        {
            TempData["Error"] = _t["msg.superAdminMustRemain"];
            return RedirectToAction(nameof(Details), new { id });
        }

        var toAdd = selected.Except(currentRoles, StringComparer.Ordinal).ToList();
        var toRemove = currentRoles.Except(selected, StringComparer.Ordinal).ToList();

        foreach (var role in toAdd)
        {
            var addResult = await _userManager.AddToRoleAsync(user, role);

            if (!addResult.Succeeded)
            {
                SetIdentityError(addResult);
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        foreach (var role in toRemove)
        {
            var removeResult = await _userManager.RemoveFromRoleAsync(user, role);

            if (!removeResult.Succeeded)
            {
                SetIdentityError(removeResult);
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        // Force existing cookies to be revalidated so the new roles apply promptly.
        await _userManager.UpdateSecurityStampAsync(user);

        await _auditService.LogAsync(
            "UserRolesUpdated",
            nameof(ApplicationUser),
            user.Id,
            $"Roles for '{user.Email}': [{string.Join(", ", currentRoles)}] -> [{string.Join(", ", selected)}].",
            cancellationToken);

        TempData["Success"] = _t["msg.rolesUpdated"];
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(string id, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id);

        if (user is null)
        {
            return NotFound();
        }

        var currentUserId = _userManager.GetUserId(User);

        if (string.Equals(currentUserId, user.Id, StringComparison.Ordinal))
        {
            TempData["Error"] = _t["msg.cannotDeactivateSelf"];
            return RedirectToAction(nameof(Details), new { id });
        }

        if (user.IsActive && await _userManager.IsInRoleAsync(user, Roles.SuperAdmin)
            && !await AnotherActiveSuperAdminExistsAsync(user.Id, cancellationToken))
        {
            TempData["Error"] = _t["msg.superAdminMustRemain"];
            return RedirectToAction(nameof(Details), new { id });
        }

        user.IsActive = !user.IsActive;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            SetIdentityError(result);
            return RedirectToAction(nameof(Details), new { id });
        }

        await _userManager.UpdateSecurityStampAsync(user);

        await _auditService.LogAsync(
            user.IsActive ? "UserActivated" : "UserDeactivated",
            nameof(ApplicationUser),
            user.Id,
            $"Account '{user.Email}' active={user.IsActive}.",
            cancellationToken);

        TempData["Success"] = user.IsActive
            ? _t["msg.accountActivated"]
            : _t["msg.accountDeactivated"];

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(string id, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id);

        if (user is null)
        {
            return NotFound();
        }

        if (!await _userManager.IsLockedOutAsync(user))
        {
            TempData["Success"] = _t["msg.accountNotLocked"];
            return RedirectToAction(nameof(Details), new { id });
        }

        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);

        await _auditService.LogAsync(
            "UserUnlocked",
            nameof(ApplicationUser),
            user.Id,
            $"Lockout cleared for '{user.Email}'.",
            cancellationToken);

        TempData["Success"] = _t["msg.accountUnlocked"];
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(
        ResetUserPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(model.UserId);

        if (user is null)
        {
            return NotFound();
        }

        // Never show the form for your own account: use the profile page instead.
        if (string.Equals(_userManager.GetUserId(User), user.Id, StringComparison.Ordinal))
        {
            TempData["Error"] = _t["msg.changeOwnPasswordFromProfile"];
            return RedirectToAction(nameof(Details), new { id = user.Id });
        }

        if (!ModelState.IsValid)
        {
            var message = string.Join(
                "; ",
                ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => ValidationMessageMap.Translate(e.ErrorMessage, _t))
                    .Where(m => !string.IsNullOrWhiteSpace(m)));

            TempData["Error"] = string.IsNullOrWhiteSpace(message)
                ? _t["val.passwordNotValid"]
                : message;

            return RedirectToAction(nameof(Details), new { id = user.Id });
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

        if (!result.Succeeded)
        {
            SetIdentityError(result);
            return RedirectToAction(nameof(Details), new { id = user.Id });
        }

        await _userManager.UpdateSecurityStampAsync(user);

        await _auditService.LogAsync(
            "UserPasswordReset",
            nameof(ApplicationUser),
            user.Id,
            $"Password reset for '{user.Email}' by an administrator.",
            cancellationToken);

        TempData["Success"] = _t["msg.passwordResetShared"];
        return RedirectToAction(nameof(Details), new { id = user.Id });
    }

    private async Task<bool> AnotherActiveSuperAdminExistsAsync(
        string excludedUserId,
        CancellationToken cancellationToken)
    {
        var superAdminRoleId = await _dbContext.Roles
            .Where(r => r.Name == Roles.SuperAdmin)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (superAdminRoleId is null)
        {
            return false;
        }

        return await (
                from userRole in _dbContext.UserRoles.AsNoTracking()
                join u in _dbContext.Users.AsNoTracking() on userRole.UserId equals u.Id
                where userRole.RoleId == superAdminRoleId
                    && u.IsActive
                    && u.Id != excludedUserId
                select u.Id)
            .AnyAsync(cancellationToken);
    }

    private void AddIdentityErrorsToModelState(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }

    private void SetIdentityError(IdentityResult result)
    {
        TempData["Error"] = string.Join(
            "; ",
            result.Errors.Select(e => ValidationMessageMap.Translate(e.Description, _t)));
    }
}
