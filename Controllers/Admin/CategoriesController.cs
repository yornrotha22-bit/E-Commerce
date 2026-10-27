using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
public class CategoriesController : Controller
{
    private const int PageSize = 20;

    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ITranslator _t;

    public CategoriesController(
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
        int page = 1,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        var effectivePageSize = AdminPageSize.Normalize(pageSize, PageSize);

        var query = _dbContext.Categories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            
var pattern = SearchPattern.Contains(term);
            query = query.Where(c =>
                EF.Functions.ILike(c.Name, pattern)
                || EF.Functions.ILike(c.Slug, pattern));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)effectivePageSize);
        page = Math.Min(page, totalPages);

        var items = await query
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Skip((page - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(c => new AdminCategoryRow
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                ParentName = c.Parent != null ? c.Parent.Name : null,
                IsActive = c.IsActive,
                DisplayOrder = c.DisplayOrder,
                ProductCount = c.Products.Count,
                ChildCount = c.Children.Count
            })
            .ToListAsync(cancellationToken);

        ViewData["Title"] = _t["admin.nav.categories"];
        ViewData["Active"] = "Categories";
        ViewData["Search"] = search;

        return View(new PagedList<AdminCategoryRow>(items, page, effectivePageSize, totalItems));
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        ViewData["Title"] = _t["adm.newCategory"];
        ViewData["Active"] = "Categories";

        var model = new CategoryFormViewModel();
        await PopulateParentOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryFormViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = _t["adm.newCategory"];
        ViewData["Active"] = "Categories";

        await NormalizeAndValidateAsync(model, cancellationToken);

        if (!ModelState.IsValid)
        {
            await PopulateParentOptionsAsync(model, cancellationToken);
            return View(model);
        }

        var category = new Category
        {
            Name = model.Name.Trim(),
            Slug = model.Slug!,
            Description = model.Description?.Trim(),
            ImageUrl = model.ImageUrl?.Trim(),
            ParentId = model.ParentId,
            DisplayOrder = model.DisplayOrder,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            "Create",
            nameof(Category),
            category.Id.ToString(),
            $"Category '{category.Name}' created.",
            cancellationToken);

        TempData["Success"] = _t.Format("msg.categoryCreated", category.Name);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var category = await _dbContext.Categories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category is null)
        {
            return NotFound();
        }

        ViewData["Title"] = _t["adm.editCategory"];
        ViewData["Active"] = "Categories";

        var model = new CategoryFormViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            ImageUrl = category.ImageUrl,
            ParentId = category.ParentId,
            DisplayOrder = category.DisplayOrder,
            IsActive = category.IsActive
        };

        await PopulateParentOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CategoryFormViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = _t["adm.editCategory"];
        ViewData["Active"] = "Categories";

        if (id != model.Id)
        {
            return BadRequest();
        }

        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category is null)
        {
            return NotFound();
        }

        await NormalizeAndValidateAsync(model, cancellationToken);

        if (!ModelState.IsValid)
        {
            await PopulateParentOptionsAsync(model, cancellationToken);
            return View(model);
        }

        category.Name = model.Name.Trim();
        category.Slug = model.Slug!;
        category.Description = model.Description?.Trim();
        category.ImageUrl = model.ImageUrl?.Trim();
        category.ParentId = model.ParentId;
        category.DisplayOrder = model.DisplayOrder;
        category.IsActive = model.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            "Update",
            nameof(Category),
            category.Id.ToString(),
            $"Category '{category.Name}' updated.",
            cancellationToken);

        TempData["Success"] = _t.Format("msg.categoryUpdated", category.Name);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id, CancellationToken cancellationToken)
    {
        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category is null)
        {
            return NotFound();
        }

        category.IsActive = !category.IsActive;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            category.IsActive ? "Activate" : "Deactivate",
            nameof(Category),
            category.Id.ToString(),
            $"Category '{category.Name}' active={category.IsActive}.",
            cancellationToken);

        TempData["Success"] = category.IsActive
            ? _t.Format("msg.categoryActivated", category.Name)
            : _t.Format("msg.categoryDeactivated", category.Name);

        return RedirectToAction(nameof(Index));
    }

    private async Task NormalizeAndValidateAsync(CategoryFormViewModel model, CancellationToken cancellationToken)
    {
        model.Name = (model.Name ?? string.Empty).Trim();
        model.Slug = string.IsNullOrWhiteSpace(model.Slug)
            ? DbInitializer.Slugify(model.Name)
            : DbInitializer.Slugify(model.Slug);

        if (string.IsNullOrWhiteSpace(model.Slug))
        {
            ModelState.AddModelError(nameof(model.Slug), "Enter a slug using letters or numbers.");
            return;
        }

        var nameTaken = await _dbContext.Categories
            .AnyAsync(c => c.Id != model.Id && c.Name.ToLower() == model.Name.ToLower(), cancellationToken);

        if (nameTaken)
        {
            ModelState.AddModelError(nameof(model.Name), "Another category already uses this name.");
        }

        var slugTaken = await _dbContext.Categories
            .AnyAsync(c => c.Id != model.Id && c.Slug == model.Slug, cancellationToken);

        if (slugTaken)
        {
            ModelState.AddModelError(nameof(model.Slug), "Another category already uses this slug.");
        }

        if (model.ParentId.HasValue)
        {
            if (model.ParentId.Value == model.Id)
            {
                ModelState.AddModelError(nameof(model.ParentId), "A category cannot be its own parent.");
                return;
            }

            var parentExists = await _dbContext.Categories
                .AnyAsync(c => c.Id == model.ParentId.Value, cancellationToken);

            if (!parentExists)
            {
                ModelState.AddModelError(nameof(model.ParentId), "The selected parent category does not exist.");
                return;
            }

            if (model.Id != 0 && await IsDescendantAsync(model.Id, model.ParentId.Value, cancellationToken))
            {
                ModelState.AddModelError(nameof(model.ParentId), "A category cannot be moved under one of its own sub-categories.");
            }
        }
    }

    /// <summary>
    /// Walks up the parent chain to make sure the new parent is not a descendant.
    /// </summary>
    private async Task<bool> IsDescendantAsync(int categoryId, int candidateParentId, CancellationToken cancellationToken)
    {
        int? current = candidateParentId;
        var guard = 0;

        while (current.HasValue && guard++ < 100)
        {
            if (current.Value == categoryId)
            {
                return true;
            }

            current = await _dbContext.Categories
                .Where(c => c.Id == current.Value)
                .Select(c => c.ParentId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return false;
    }

    private async Task PopulateParentOptionsAsync(CategoryFormViewModel model, CancellationToken cancellationToken)
    {
        var categories = await _dbContext.Categories.AsNoTracking()
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);

        var byParent = categories.ToLookup(c => c.ParentId);

        var options = new List<SelectListItem>
        {
            new("— No parent (top level) —", string.Empty, model.ParentId is null)
        };

        AddOptions(options, byParent, null, 0, model.Id);
        model.ParentOptions = options;
    }

    private static void AddOptions(
        List<SelectListItem> target,
        ILookup<int?, Category> byParent,
        int? parentId,
        int depth,
        int excludeId)
    {
        foreach (var category in byParent[parentId].OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name))
        {
            if (category.Id == excludeId)
            {
                continue;
            }

            var prefix = depth == 0 ? string.Empty : new string('·', depth) + " ";
            target.Add(new SelectListItem(
                $"{prefix}{category.Name}",
                category.Id.ToString(),
                false));

            AddOptions(target, byParent, category.Id, depth + 1, excludeId);
        }
    }
}
