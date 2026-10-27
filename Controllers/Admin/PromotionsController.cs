using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RothaShop.Data;
using RothaShop.Localization;
using RothaShop.Models;
using RothaShop.Security;
using RothaShop.Services;
using RothaShop.ViewModels;
using RothaShop.ViewModels.Admin;

namespace RothaShop.Controllers.Admin;

[Area("Admin")]
[Authorize(Policy = Policies.ManagePromotions)]
public class PromotionsController : Controller
{
    private const int PageSize = 20;

    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ITranslator _t;

    public PromotionsController(
        ApplicationDbContext dbContext,
        IAuditService auditService,
        ITranslator translator)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _t = translator;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        bool? isActive,
        int page = 1,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        var effectivePageSize = AdminPageSize.Normalize(pageSize, PageSize);

        var query = _dbContext.Coupons.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            
var pattern = SearchPattern.Contains(term);
            query = query.Where(c =>
                EF.Functions.ILike(c.Code, pattern)
                || (c.Description != null && EF.Functions.ILike(c.Description, pattern)));
        }

        if (isActive.HasValue)
        {
            query = query.Where(c => c.IsActive == isActive.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)effectivePageSize);
        page = Math.Min(page, totalPages);

        var items = await query
            .OrderByDescending(c => c.Id)
            .Skip((page - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(c => new AdminCouponRow
            {
                Id = c.Id,
                Code = c.Code,
                DiscountType = c.DiscountType,
                DiscountValue = c.DiscountValue,
                MinimumOrderAmount = c.MinimumOrderAmount,
                UsageLimit = c.UsageLimit,
                UsedCount = c.UsedCount,
                StartsAt = c.StartsAt,
                EndsAt = c.EndsAt,
                IsActive = c.IsActive
            })
            .ToListAsync(cancellationToken);

        ViewData["Title"] = _t["admin.nav.promotions"];
        ViewData["Active"] = "Promotions";
        ViewData["Search"] = search;
        ViewData["IsActive"] = isActive;

        return View(new PagedList<AdminCouponRow>(items, page, effectivePageSize, totalItems));
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = _t["adm.newCoupon"];
        ViewData["Active"] = "Promotions";
        return View(new CouponFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CouponFormViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = _t["adm.newCoupon"];
        ViewData["Active"] = "Promotions";

        await NormalizeAndValidateAsync(model, cancellationToken);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var coupon = new Coupon
        {
            Code = model.Code,
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
            DiscountType = model.DiscountType,
            DiscountValue = model.DiscountValue,
            MinimumOrderAmount = model.MinimumOrderAmount,
            MaxDiscountAmount = model.MaxDiscountAmount,
            StartsAt = ToUtcStart(model.StartsAt),
            EndsAt = model.EndsAt.HasValue ? ToUtcEnd(model.EndsAt.Value) : null,
            UsageLimit = model.UsageLimit,
            UsedCount = 0,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Coupons.Add(coupon);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            "Create",
            nameof(Coupon),
            coupon.Id.ToString(),
            $"Coupon '{coupon.Code}' created.",
            cancellationToken);

        TempData["Success"] = _t.Format("msg.couponCreated", coupon.Code);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var coupon = await _dbContext.Coupons.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (coupon is null)
        {
            return NotFound();
        }

        ViewData["Title"] = _t["adm.editCoupon"];
        ViewData["Active"] = "Promotions";

        return View(new CouponFormViewModel
        {
            Id = coupon.Id,
            Code = coupon.Code,
            Description = coupon.Description,
            DiscountType = coupon.DiscountType,
            DiscountValue = coupon.DiscountValue,
            MinimumOrderAmount = coupon.MinimumOrderAmount,
            MaxDiscountAmount = coupon.MaxDiscountAmount,
            StartsAt = coupon.StartsAt,
            EndsAt = coupon.EndsAt,
            UsageLimit = coupon.UsageLimit,
            IsActive = coupon.IsActive,
            UsedCount = coupon.UsedCount
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CouponFormViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = _t["adm.editCoupon"];
        ViewData["Active"] = "Promotions";

        if (id != model.Id)
        {
            return BadRequest();
        }

        var coupon = await _dbContext.Coupons.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (coupon is null)
        {
            return NotFound();
        }

        model.UsedCount = coupon.UsedCount;
        await NormalizeAndValidateAsync(model, cancellationToken);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        coupon.Code = model.Code;
        coupon.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        coupon.DiscountType = model.DiscountType;
        coupon.DiscountValue = model.DiscountValue;
        coupon.MinimumOrderAmount = model.MinimumOrderAmount;
        coupon.MaxDiscountAmount = model.MaxDiscountAmount;
        coupon.StartsAt = ToUtcStart(model.StartsAt);
        coupon.EndsAt = model.EndsAt.HasValue ? ToUtcEnd(model.EndsAt.Value) : null;
        coupon.UsageLimit = model.UsageLimit;
        coupon.IsActive = model.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            "Update",
            nameof(Coupon),
            coupon.Id.ToString(),
            $"Coupon '{coupon.Code}' updated.",
            cancellationToken);

        TempData["Success"] = _t.Format("msg.couponUpdated", coupon.Code);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id, CancellationToken cancellationToken)
    {
        var coupon = await _dbContext.Coupons.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (coupon is null)
        {
            return NotFound();
        }

        coupon.IsActive = !coupon.IsActive;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            coupon.IsActive ? "Activate" : "Deactivate",
            nameof(Coupon),
            coupon.Id.ToString(),
            $"Coupon '{coupon.Code}' active={coupon.IsActive}.",
            cancellationToken);

        TempData["Success"] = coupon.IsActive
            ? _t.Format("msg.couponActivated", coupon.Code)
            : _t.Format("msg.couponDeactivated", coupon.Code);

        return RedirectToAction(nameof(Index));
    }

    private async Task NormalizeAndValidateAsync(CouponFormViewModel model, CancellationToken cancellationToken)
    {
        model.Code = (model.Code ?? string.Empty).Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(model.Code))
        {
            ModelState.AddModelError(nameof(model.Code), "Enter a coupon code.");
            return;
        }

        if (model.DiscountType == DiscountType.Percentage && model.DiscountValue > 100m)
        {
            ModelState.AddModelError(nameof(model.DiscountValue), "A percentage discount cannot exceed 100.");
        }

        if (model.EndsAt.HasValue && ToUtcEnd(model.EndsAt.Value) < ToUtcStart(model.StartsAt))
        {
            ModelState.AddModelError(nameof(model.EndsAt), "The end date must be after the start date.");
        }

        if (model.UsageLimit.HasValue && model.UsageLimit.Value < model.UsedCount)
        {
            ModelState.AddModelError(
                nameof(model.UsageLimit),
                _t.Format("msg.couponUsageLimitLower", model.UsedCount));
        }

        var codeTaken = await _dbContext.Coupons
            .AnyAsync(c => c.Id != model.Id && c.Code == model.Code, cancellationToken);

        if (codeTaken)
        {
            ModelState.AddModelError(nameof(model.Code), "Another coupon already uses this code.");
        }
    }

    private static DateTime ToUtcStart(DateTime value) =>
        DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

    private static DateTime ToUtcEnd(DateTime value) =>
        DateTime.SpecifyKind(value.Date, DateTimeKind.Utc).AddDays(1).AddTicks(-1);
}
