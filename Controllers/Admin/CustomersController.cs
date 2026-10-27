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

[Area("Admin")]
[Authorize(Policy = Policies.ManageCustomers)]
public class CustomersController : Controller
{
    private const int PageSize = 20;

    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly ITranslator _t;

    public CustomersController(
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
        int page = 1,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        var effectivePageSize = AdminPageSize.Normalize(pageSize, PageSize);

        var customerRoleId = await _dbContext.Roles
            .Where(r => r.Name == Roles.Customer)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var model = new AdminCustomerListViewModel
        {
            Search = search,
            Page = page,
            PageSize = effectivePageSize
        };

        if (customerRoleId is null)
        {
            ViewData["Title"] = "Customers";
            ViewData["Active"] = "Customers";
            return View(model);
        }

        var query = from user in _dbContext.Users.AsNoTracking()
                    join userRole in _dbContext.UserRoles on user.Id equals userRole.UserId
                    where userRole.RoleId == customerRoleId
                    select user;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            
var pattern = SearchPattern.Contains(term);
            query = query.Where(u =>
                EF.Functions.ILike(u.FullName, pattern)
                || (u.Email != null && EF.Functions.ILike(u.Email, pattern))
                || (u.PhoneNumber != null && EF.Functions.ILike(u.PhoneNumber, pattern)));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)effectivePageSize);
        page = Math.Min(page, totalPages);

        model.Page = page;
        model.TotalItems = totalItems;
        model.Items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(u => new AdminCustomerRow
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? string.Empty,
                PhoneNumber = u.PhoneNumber,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                OrderCount = u.Orders.Count(o => o.OrderStatus != OrderStatus.Cancelled),
                PaidTotal = u.Orders
                    .Where(o => o.PaymentStatus == PaymentStatus.Paid)
                    .Sum(o => (decimal?)o.TotalAmount) ?? 0m,
                LastOrderAt = u.Orders
                    .Select(o => (DateTime?)o.CreatedAt)
                    .Max()
            })
            .ToListAsync(cancellationToken);

        ViewData["Title"] = "Customers";
        ViewData["Active"] = "Customers";

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (customer is null)
        {
            return NotFound();
        }

        if (!await _userManager.IsInRoleAsync(customer, Roles.Customer))
        {
            return NotFound();
        }

        var addresses = await _dbContext.Addresses.AsNoTracking()
            .Where(a => a.UserId == id)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.Id)
            .ToListAsync(cancellationToken);

        var orders = await _dbContext.Orders.AsNoTracking()
            .Where(o => o.UserId == id)
            .OrderByDescending(o => o.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        ViewData["Title"] = "Customer details";
        ViewData["Active"] = "Customers";

        return View(new AdminCustomerDetailsViewModel
        {
            Customer = customer,
            Addresses = addresses,
            Orders = orders
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(string id, CancellationToken cancellationToken)
    {
        var customer = await _userManager.FindByIdAsync(id);

        if (customer is null || !await _userManager.IsInRoleAsync(customer, Roles.Customer))
        {
            return NotFound();
        }

        customer.IsActive = !customer.IsActive;
        var result = await _userManager.UpdateAsync(customer);

        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join("; ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Details), new { id });
        }

        await _auditService.LogAsync(
            customer.IsActive ? "CustomerActivated" : "CustomerDeactivated",
            nameof(ApplicationUser),
            customer.Id,
            $"Customer '{customer.Email}' active={customer.IsActive}.",
            cancellationToken);

        TempData["Success"] = customer.IsActive
            ? _t["msg.customerActivated"]
            : _t["msg.customerDeactivated"];

        return RedirectToAction(nameof(Details), new { id });
    }
}
