using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RothaShop.Data;
using RothaShop.Localization;
using RothaShop.Models;

namespace RothaShop.Controllers;

[Authorize]
public class ReviewsController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITranslator _t;

    public ReviewsController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ITranslator translator)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _t = translator;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        int productId,
        int orderItemId,
        int rating,
        string? title,
        string? comment,
        CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);

        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var text = (comment ?? string.Empty).Trim();

        if (rating < 1 || rating > 5)
        {
            TempData["ReviewError"] = _t["msg.ratingRange"];
            return RedirectToAction("Details", "Products", new { id = productId });
        }

        if (text.Length < 10)
        {
            TempData["ReviewError"] = _t["msg.reviewTooShort"];
            return RedirectToAction("Details", "Products", new { id = productId });
        }

        if (text.Length > 2000)
        {
            TempData["ReviewError"] = _t["msg.reviewTooLong"];
            return RedirectToAction("Details", "Products", new { id = productId });
        }

        // Only a delivered purchase of this exact product can be reviewed.
        var orderItem = await _dbContext.OrderItems.AsNoTracking()
            .Include(i => i.Order)
            .FirstOrDefaultAsync(
                i => i.Id == orderItemId
                     && i.ProductId == productId
                     && i.Order!.UserId == userId
                     && i.Order.OrderStatus == OrderStatus.Delivered,
                cancellationToken);

        if (orderItem is null)
        {
            TempData["ReviewError"] = _t["msg.reviewNotEligible"];
            return RedirectToAction("Details", "Products", new { id = productId });
        }

        var alreadyReviewed = await _dbContext.ProductReviews
            .AnyAsync(r => r.UserId == userId && r.ProductId == productId, cancellationToken);

        if (alreadyReviewed)
        {
            TempData["ReviewError"] = _t["msg.reviewDuplicate"];
            return RedirectToAction("Details", "Products", new { id = productId });
        }

        var review = new ProductReview
        {
            ProductId = productId,
            UserId = userId,
            OrderItemId = orderItem.Id,
            Rating = rating,
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim()[..Math.Min(title.Trim().Length, 150)],
            Comment = text,
            IsApproved = false,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ProductReviews.Add(review);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Unique index (UserId, ProductId) protects against double submission.
            TempData["ReviewError"] = _t["msg.reviewDuplicate"];
            return RedirectToAction("Details", "Products", new { id = productId });
        }

        TempData["ReviewSuccess"] = _t["msg.reviewSubmitted"];

        return RedirectToAction("Details", "Products", new { id = productId });
    }
}
