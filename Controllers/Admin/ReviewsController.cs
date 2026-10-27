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
[Authorize(Policy = Policies.ManageCatalog)]
public class ReviewsController : Controller
{
    private const int PageSize = 20;

    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ITranslator _t;

    public ReviewsController(
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
        string? status,
        int page = 1,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        var effectivePageSize = AdminPageSize.Normalize(pageSize, PageSize);
        var filter = string.IsNullOrWhiteSpace(status) ? "pending" : status.ToLowerInvariant();

        var query = _dbContext.ProductReviews.AsNoTracking();

        query = filter switch
        {
            "approved" => query.Where(r => r.IsApproved),
            "all" => query,
            _ => query.Where(r => !r.IsApproved)
        };

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)effectivePageSize);
        page = Math.Min(page, totalPages);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(r => new AdminReviewRow
            {
                Id = r.Id,
                ProductId = r.ProductId,
                ProductName = r.Product!.Name,
                CustomerName = r.User != null ? r.User.FullName : "Customer",
                Rating = r.Rating,
                Title = r.Title,
                Comment = r.Comment,
                IsApproved = r.IsApproved,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(cancellationToken);

        ViewData["Title"] = _t["admin.nav.reviews"];
        ViewData["Active"] = "Reviews";
        ViewData["Status"] = filter;

        return View(new PagedList<AdminReviewRow>(items, page, effectivePageSize, totalItems));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken)
    {
        var review = await _dbContext.ProductReviews.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (review is null)
        {
            return NotFound();
        }

        review.IsApproved = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            "ReviewApproved",
            nameof(ProductReview),
            review.Id.ToString(),
            $"Review {review.Id} approved.",
            cancellationToken);

        TempData["Success"] = _t["msg.reviewApproved"];
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, CancellationToken cancellationToken)
    {
        var review = await _dbContext.ProductReviews.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (review is null)
        {
            return NotFound();
        }

        _dbContext.ProductReviews.Remove(review);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            "ReviewRejected",
            nameof(ProductReview),
            id.ToString(),
            "Review rejected and deleted.",
            cancellationToken);

        TempData["Success"] = _t["msg.reviewRejected"];
        return RedirectToAction(nameof(Index));
    }
}
