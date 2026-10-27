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
public class BrandsController : Controller
{
    private const int PageSize = 15;

    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ITranslator _t;
    private readonly IImageService _imageService;

    public BrandsController(
        ApplicationDbContext dbContext,
        IAuditService auditService,
        ITranslator translator,
        IImageService imageService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _t = translator;
        _imageService = imageService;
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

        var query = _dbContext.Brands.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            
var pattern = SearchPattern.Contains(term);
            query = query.Where(b =>
                EF.Functions.ILike(b.Name, pattern)
                || EF.Functions.ILike(b.Slug, pattern));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)effectivePageSize);
        page = Math.Min(page, totalPages);

        var items = await query
            .OrderBy(b => b.Name)
            .Skip((page - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(b => new AdminBrandRow
            {
                Id = b.Id,
                Name = b.Name,
                Slug = b.Slug,
                LogoUrl = b.LogoUrl,
                IsActive = b.IsActive,
                ProductCount = b.Products.Count,
                CreatedAt = b.CreatedAt
            })
            .ToListAsync(cancellationToken);

        ViewData["Title"] = _t["admin.nav.brands"];
        ViewData["Active"] = "Brands";
        ViewData["Search"] = search;

        return View(new PagedList<AdminBrandRow>(items, page, effectivePageSize, totalItems));
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = _t["adm.newBrand"];
        ViewData["Active"] = "Brands";
        return View(new BrandFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(ImageService.MaxFileSizeBytes + 64 * 1024)]
    public async Task<IActionResult> Create(BrandFormViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = _t["adm.newBrand"];
        ViewData["Active"] = "Brands";

        await NormalizeAndValidateAsync(model, cancellationToken);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.LogoFile is { Length: > 0 })
        {
            model.LogoUrl = await _imageService.SaveBrandImageAsync(model.LogoFile, cancellationToken);
            if (model.LogoUrl is null)
            {
                ModelState.AddModelError(nameof(model.LogoFile), _imageService.LastError ?? "The logo could not be saved.");
                return View(model);
            }
        }

        var brand = new Brand
        {
            Name = model.Name.Trim(),
            Slug = model.Slug!,
            Description = model.Description?.Trim(),
            LogoUrl = model.LogoUrl,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Brands.Add(brand);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            "Create",
            nameof(Brand),
            brand.Id.ToString(),
            $"Brand '{brand.Name}' created.",
            cancellationToken);

        TempData["Success"] = _t.Format("msg.brandCreated", brand.Name);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var brand = await _dbContext.Brands.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (brand is null)
        {
            return NotFound();
        }

        ViewData["Title"] = _t["adm.editBrand"];
        ViewData["Active"] = "Brands";

        return View(new BrandFormViewModel
        {
            Id = brand.Id,
            Name = brand.Name,
            Slug = brand.Slug,
            Description = brand.Description,
            LogoUrl = brand.LogoUrl,
            IsActive = brand.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(ImageService.MaxFileSizeBytes + 64 * 1024)]
    public async Task<IActionResult> Edit(int id, BrandFormViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = _t["adm.editBrand"];
        ViewData["Active"] = "Brands";

        if (id != model.Id)
        {
            return BadRequest();
        }

        var brand = await _dbContext.Brands.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (brand is null)
        {
            return NotFound();
        }

        model.LogoUrl = brand.LogoUrl;

        await NormalizeAndValidateAsync(model, cancellationToken);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string? replacementLogoUrl = null;
        if (model.LogoFile is { Length: > 0 })
        {
            replacementLogoUrl = await _imageService.SaveBrandImageAsync(model.LogoFile, cancellationToken);
            if (replacementLogoUrl is null)
            {
                ModelState.AddModelError(nameof(model.LogoFile), _imageService.LastError ?? "The logo could not be saved.");
                return View(model);
            }
        }

        var previousName = brand.Name;
        string? previousLogoUrlToDelete = null;

        brand.Name = model.Name.Trim();
        brand.Slug = model.Slug!;
        brand.Description = model.Description?.Trim();
        if (replacementLogoUrl is not null)
        {
            previousLogoUrlToDelete = brand.LogoUrl;
            brand.LogoUrl = replacementLogoUrl;
        }
        else if (model.RemoveLogo)
        {
            previousLogoUrlToDelete = brand.LogoUrl;
            brand.LogoUrl = null;
        }
        brand.IsActive = model.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (previousLogoUrlToDelete is not null)
        {
            _imageService.TryDeleteBrandImage(previousLogoUrlToDelete);
        }

        await _auditService.LogAsync(
            "Update",
            nameof(Brand),
            brand.Id.ToString(),
            $"Brand renamed from '{previousName}' to '{brand.Name}'.",
            cancellationToken);

        TempData["Success"] = _t.Format("msg.brandUpdated", brand.Name);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id, CancellationToken cancellationToken)
    {
        var brand = await _dbContext.Brands.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (brand is null)
        {
            return NotFound();
        }

        brand.IsActive = !brand.IsActive;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            brand.IsActive ? "Activate" : "Deactivate",
            nameof(Brand),
            brand.Id.ToString(),
            $"Brand '{brand.Name}' active={brand.IsActive}.",
            cancellationToken);

        TempData["Success"] = brand.IsActive
            ? _t.Format("msg.brandActivated", brand.Name)
            : _t.Format("msg.brandDeactivated", brand.Name);

        return RedirectToAction(nameof(Index));
    }

    private async Task NormalizeAndValidateAsync(BrandFormViewModel model, CancellationToken cancellationToken)
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

        var nameTaken = await _dbContext.Brands
            .AnyAsync(b => b.Id != model.Id && b.Name.ToLower() == model.Name.ToLower(), cancellationToken);

        if (nameTaken)
        {
            ModelState.AddModelError(nameof(model.Name), "Another brand already uses this name.");
        }

        var slugTaken = await _dbContext.Brands
            .AnyAsync(b => b.Id != model.Id && b.Slug == model.Slug, cancellationToken);

        if (slugTaken)
        {
            ModelState.AddModelError(nameof(model.Slug), "Another brand already uses this slug.");
        }
    }
}
