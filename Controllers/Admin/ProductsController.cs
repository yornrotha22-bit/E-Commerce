using System.Security.Claims;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
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
public class ProductsController : Controller
{
    private const int PageSize = 15;
    private const long MaxImportFileBytes = 5 * 1024 * 1024;
    private const int MaxImportRows = 5000;
    private static readonly string[] ProductCsvHeaders =
    {
        "ProductName", "Slug", "Description", "BrandSlug", "CategorySlug", "MainImageUrl",
        "IsActive", "IsFeatured", "SKU", "Color", "Storage", "Price", "CompareAtPrice",
        "StockQuantity", "LowStockThreshold", "VariantIsActive"
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly IImageService _imageService;
    private readonly IAuditService _auditService;
    private readonly ITranslator _t;

    public ProductsController(
        ApplicationDbContext dbContext,
        IImageService imageService,
        IAuditService auditService,
        ITranslator translator)
    {
        _dbContext = dbContext;
        _imageService = imageService;
        _auditService = auditService;
        _t = translator;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        int? categoryId,
        int? brandId,
        bool? isActive,
        string? sort,
        int page = 1,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        var effectivePageSize = AdminPageSize.Normalize(pageSize, PageSize);

        var query = _dbContext.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var pattern = SearchPattern.Contains(term);
            query = query.Where(p =>
                EF.Functions.ILike(p.Name, pattern)
                || EF.Functions.ILike(p.Slug, pattern)
                || EF.Functions.ILike(p.Brand!.Name, pattern)
                || p.Variants.Any(v => EF.Functions.ILike(v.SKU, pattern)));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (brandId.HasValue)
        {
            query = query.Where(p => p.BrandId == brandId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(p => p.IsActive == isActive.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)effectivePageSize);
        page = Math.Min(page, totalPages);

        query = sort switch
        {
            "name" => query.OrderBy(p => p.Name),
            "price_asc" => query.OrderBy(p => p.Variants.Min(v => (decimal?)v.Price) ?? 0),
            "price_desc" => query.OrderByDescending(p => p.Variants.Min(v => (decimal?)v.Price) ?? 0),
            "stock" => query.OrderBy(p => p.Variants.Sum(v => (int?)v.StockQuantity) ?? 0),
            "oldest" => query.OrderBy(p => p.CreatedAt),
            _ => query.OrderByDescending(p => p.CreatedAt)
        };

        var items = await query
            .Skip((page - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(p => new AdminProductRow
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                BrandName = p.Brand != null ? p.Brand.Name : string.Empty,
                CategoryName = p.Category != null ? p.Category.Name : string.Empty,
                MainImageUrl = p.MainImageUrl
                    ?? p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault(),
                IsActive = p.IsActive,
                IsFeatured = p.IsFeatured,
                IsVisibleOnStorefront = p.IsActive
                    && p.Brand != null && p.Brand.IsActive
                    && p.Category != null && p.Category.IsActive
                    && p.Variants.Any(v => v.IsActive),
                VariantCount = p.Variants.Count,
                TotalStock = p.Variants.Sum(v => (int?)v.StockQuantity) ?? 0,
                MinPrice = p.Variants.Select(v => (decimal?)v.Price).Min() ?? 0,
                MaxPrice = p.Variants.Select(v => (decimal?)v.Price).Max() ?? 0,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var model = new AdminProductListViewModel
        {
            Search = search,
            CategoryId = categoryId,
            BrandId = brandId,
            IsActive = isActive,
            Sort = string.IsNullOrWhiteSpace(sort) ? "newest" : sort,
            Page = page,
            PageSize = effectivePageSize,
            TotalItems = totalItems,
            Items = items,
            CategoryOptions = await GetCategoryOptionsAsync(cancellationToken),
            BrandOptions = await GetBrandOptionsAsync(cancellationToken),
            StatusOptions = new List<SelectListItem>
            {
                new(_t["adm.allStatuses"], string.Empty, isActive is null),
                new(_t["adm.filterActive"], "true", isActive == true),
                new(_t["adm.filterInactive"], "false", isActive == false)
            }
        };

        ViewData["Title"] = _t["admin.nav.products"];
        ViewData["Active"] = "Products";

        return View(model);
    }

    [HttpGet]
    public IActionResult Import()
    {
        ViewData["Title"] = _t["adm.importExportProducts"];
        ViewData["Active"] = "Products";
        return View(new ProductImportViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxImportFileBytes + 64 * 1024)]
    public async Task<IActionResult> Import(ProductImportViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = _t["adm.importExportProducts"];
        ViewData["Active"] = "Products";

        if (model.File is null || model.File.Length == 0)
        {
            model.Errors = new[] { _t["adm.importChooseFile"] };
            return View(model);
        }

        if (model.File.Length > MaxImportFileBytes)
        {
            model.Errors = new[] { _t["adm.importFileTooLarge"] };
            return View(model);
        }

        var errors = new List<string>();
        var drafts = new Dictionary<string, ProductImportDraft>(StringComparer.OrdinalIgnoreCase);
        var importedSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existingSlugs = (await _dbContext.Products.AsNoTracking()
                .Select(p => p.Slug)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingSkus = (await _dbContext.ProductVariants.AsNoTracking()
                .Select(v => v.SKU)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var brands = await _dbContext.Brands.AsNoTracking()
            .Where(b => b.IsActive)
            .ToDictionaryAsync(b => b.Slug, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var categories = await _dbContext.Categories.AsNoTracking()
            .Where(c => c.IsActive)
            .ToDictionaryAsync(c => c.Slug, StringComparer.OrdinalIgnoreCase, cancellationToken);

        try
        {
            using var reader = new StreamReader(model.File.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            using var parser = new TextFieldParser(reader)
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = false
            };
            parser.SetDelimiters(",");

            if (parser.EndOfData)
            {
                errors.Add(_t["adm.importEmptyFile"]);
            }
            else
            {
                var header = parser.ReadFields() ?? Array.Empty<string>();
                var headerMap = header
                    .Select((name, index) => new { Name = name.Trim(), Index = index })
                    .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().Index, StringComparer.OrdinalIgnoreCase);
                var missingHeaders = ProductCsvHeaders.Where(h => !headerMap.ContainsKey(h)).ToArray();

                if (missingHeaders.Length > 0)
                {
                    errors.Add(_t.Format("adm.importMissingHeaders", string.Join(", ", missingHeaders)));
                }
                else
                {
                    var recordNumber = 1;

                    while (!parser.EndOfData)
                    {
                        string[]? fields;
                        try
                        {
                            fields = parser.ReadFields();
                        }
                        catch (MalformedLineException)
                        {
                            errors.Add(_t.Format("adm.importInvalidRow", parser.ErrorLineNumber));
                            continue;
                        }

                        recordNumber++;
                        if (recordNumber > MaxImportRows + 1)
                        {
                            errors.Add(_t.Format("adm.importMaxRows", MaxImportRows));
                            break;
                        }

                        if (fields is null || fields.All(string.IsNullOrWhiteSpace))
                        {
                            continue;
                        }

                        string Value(string key)
                        {
                            var index = headerMap[key];
                            return index < fields.Length ? fields[index] : string.Empty;
                        }

                        var rowErrors = new List<string>();
                        var name = Value("ProductName").Trim();
                        var slug = Value("Slug").Trim().ToLowerInvariant();
                        var description = Value("Description");
                        var brandSlug = Value("BrandSlug").Trim();
                        var categorySlug = Value("CategorySlug").Trim();
                        var imageUrl = Value("MainImageUrl").Trim();
                        var sku = Value("SKU").Trim();
                        var color = Value("Color").Trim();
                        var storage = Value("Storage").Trim();

                        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
                            rowErrors.Add("ProductName is required and must be 200 characters or fewer.");
                        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 220 || DbInitializer.Slugify(slug) != slug)
                            rowErrors.Add("Slug must be a valid URL-friendly value of 220 characters or fewer.");
                        if (description.Length > 4000)
                            rowErrors.Add("Description must be 4,000 characters or fewer.");
                        if (string.IsNullOrWhiteSpace(sku) || sku.Length > 64)
                            rowErrors.Add("SKU is required and must be 64 characters or fewer.");
                        if (color.Length > 50 || storage.Length > 50)
                            rowErrors.Add("Color and Storage must be 50 characters or fewer.");
                        if (!brands.TryGetValue(brandSlug, out var brand))
                            rowErrors.Add($"BrandSlug '{brandSlug}' does not match an active brand.");
                        if (!categories.TryGetValue(categorySlug, out var category))
                            rowErrors.Add($"CategorySlug '{categorySlug}' does not match an active category.");

                        if (!bool.TryParse(Value("IsActive"), out var isActive))
                            rowErrors.Add("IsActive must be true or false.");
                        if (!bool.TryParse(Value("IsFeatured"), out var isFeatured))
                            rowErrors.Add("IsFeatured must be true or false.");
                        if (!bool.TryParse(Value("VariantIsActive"), out var variantIsActive))
                            rowErrors.Add("VariantIsActive must be true or false.");
                        if (!decimal.TryParse(Value("Price"), NumberStyles.Number, CultureInfo.InvariantCulture, out var price)
                            || price <= 0m || price > 999_999_999m)
                            rowErrors.Add("Price must be greater than zero and use a decimal point.");

                        decimal? compareAtPrice = null;
                        var compareAtText = Value("CompareAtPrice").Trim();
                        if (!string.IsNullOrWhiteSpace(compareAtText))
                        {
                            if (!decimal.TryParse(compareAtText, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedCompareAt)
                                || parsedCompareAt < price || parsedCompareAt > 999_999_999m)
                                rowErrors.Add("CompareAtPrice must be blank or at least Price.");
                            else
                                compareAtPrice = parsedCompareAt;
                        }

                        if (!int.TryParse(Value("StockQuantity"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var stock)
                            || stock < 0)
                            rowErrors.Add("StockQuantity must be zero or greater.");
                        if (!int.TryParse(Value("LowStockThreshold"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var lowStockThreshold)
                            || lowStockThreshold < 0 || lowStockThreshold > 100_000)
                            rowErrors.Add("LowStockThreshold must be between 0 and 100000.");
                        if (!string.IsNullOrWhiteSpace(imageUrl)
                            && imageUrl.Length > 500)
                            rowErrors.Add("MainImageUrl must be 500 characters or fewer.");

                        if (rowErrors.Count > 0)
                        {
                            errors.Add(_t.Format("adm.importRowErrors", recordNumber, string.Join(" ", rowErrors)));
                            continue;
                        }

                        if (existingSlugs.Contains(slug))
                        {
                            errors.Add(_t.Format("adm.importSlugExists", recordNumber, slug));
                            continue;
                        }

                        if (existingSkus.Contains(sku) || !importedSkus.Add(sku))
                        {
                            errors.Add(_t.Format("adm.importSkuExists", recordNumber, sku));
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(imageUrl)
                            && !(imageUrl.StartsWith('/') && !imageUrl.StartsWith("//") && !imageUrl.Contains("..", StringComparison.Ordinal)
                                || Uri.TryCreate(imageUrl, UriKind.Absolute, out var imageUri)
                                    && (imageUri.Scheme == Uri.UriSchemeHttps || imageUri.Scheme == Uri.UriSchemeHttp)))
                        {
                            errors.Add(_t.Format("adm.importRowErrors", recordNumber, "MainImageUrl must be a site-relative path or an HTTP(S) URL."));
                            continue;
                        }

                        var product = new Product
                        {
                            Name = name,
                            Slug = slug,
                            Description = string.IsNullOrWhiteSpace(description) ? null : description,
                            BrandId = brand!.Id,
                            CategoryId = category!.Id,
                            MainImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl,
                            IsActive = isActive,
                            IsFeatured = isFeatured,
                            CreatedAt = DateTime.UtcNow
                        };

                        if (drafts.TryGetValue(slug, out var draft))
                        {
                            if (draft.Product.Name != product.Name
                                || draft.Product.Description != product.Description
                                || draft.Product.BrandId != product.BrandId
                                || draft.Product.CategoryId != product.CategoryId
                                || draft.Product.MainImageUrl != product.MainImageUrl
                                || draft.Product.IsActive != product.IsActive
                                || draft.Product.IsFeatured != product.IsFeatured)
                            {
                                errors.Add(_t.Format("adm.importRowErrors", recordNumber, "Rows sharing a Slug must have identical product fields."));
                                continue;
                            }
                        }
                        else
                        {
                            draft = new ProductImportDraft(product);
                            drafts.Add(slug, draft);
                        }

                        draft.Product.Variants.Add(new ProductVariant
                        {
                            SKU = sku,
                            Color = string.IsNullOrWhiteSpace(color) ? null : color,
                            Storage = string.IsNullOrWhiteSpace(storage) ? null : storage,
                            Price = price,
                            CompareAtPrice = compareAtPrice,
                            StockQuantity = stock,
                            LowStockThreshold = lowStockThreshold,
                            IsActive = variantIsActive,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }
        }
        catch (MalformedLineException)
        {
            errors.Add(_t["adm.importMalformedCsv"]);
        }
        catch (IOException)
        {
            errors.Add(_t["adm.importReadFailed"]);
        }

        if (errors.Count > 0 || drafts.Count == 0)
        {
            if (drafts.Count == 0 && errors.Count == 0)
                errors.Add(_t["adm.importNoProducts"]);

            model.Errors = errors;
            return View(model);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await using (var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            _dbContext.Products.AddRange(drafts.Values.Select(d => d.Product));
            await _dbContext.SaveChangesAsync(cancellationToken);

            var initialStock = drafts.Values
                .SelectMany(d => d.Product.Variants)
                .Where(v => v.StockQuantity > 0)
                .Select(v => new StockMovement
                {
                    ProductVariantId = v.Id,
                    QuantityChange = v.StockQuantity,
                    StockAfter = v.StockQuantity,
                    Type = StockMovementType.Inbound,
                    Reason = "Initial stock from CSV import",
                    PerformedByUserId = userId
                });
            _dbContext.StockMovements.AddRange(initialStock);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        model.ImportedProducts = drafts.Count;
        model.ImportedVariants = drafts.Values.Sum(d => d.Product.Variants.Count);
        TempData["Success"] = _t.Format("adm.importSuccess", model.ImportedProducts, model.ImportedVariants);
        await _auditService.LogAsync(
            "Import",
            nameof(Product),
            string.Empty,
            $"Imported {model.ImportedProducts} products and {model.ImportedVariants} variants from CSV.",
            cancellationToken);

        return RedirectToAction(nameof(Import));
    }

    [HttpGet]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var products = await _dbContext.Products.AsNoTracking()
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.Variants)
            .OrderBy(p => p.Slug)
            .ToListAsync(cancellationToken);

        var csv = new StringBuilder();
        AppendCsvRow(csv, ProductCsvHeaders);

        foreach (var product in products)
        {
            foreach (var variant in product.Variants.OrderBy(v => v.SKU))
            {
                AppendCsvRow(csv, new string?[]
                {
                    product.Name,
                    product.Slug,
                    product.Description,
                    product.Brand?.Slug,
                    product.Category?.Slug,
                    product.MainImageUrl,
                    product.IsActive.ToString().ToLowerInvariant(),
                    product.IsFeatured.ToString().ToLowerInvariant(),
                    variant.SKU,
                    variant.Color,
                    variant.Storage,
                    variant.Price.ToString(CultureInfo.InvariantCulture),
                    variant.CompareAtPrice?.ToString(CultureInfo.InvariantCulture),
                    variant.StockQuantity.ToString(CultureInfo.InvariantCulture),
                    variant.LowStockThreshold.ToString(CultureInfo.InvariantCulture),
                    variant.IsActive.ToString().ToLowerInvariant()
                });
            }
        }

        var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        await _auditService.LogAsync(
            "Export",
            nameof(Product),
            string.Empty,
            $"Exported {products.Count} products to CSV.",
            cancellationToken);

        return File(content, "text/csv; charset=utf-8", $"kstore-products-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet]
    public IActionResult Template()
    {
        var csv = new StringBuilder();
        AppendCsvRow(csv, ProductCsvHeaders);
        AppendCsvRow(csv, new string?[]
        {
            "Example Phone", "example-phone", "Product description", "brand-slug", "category-slug", "/uploads/products/example.webp",
            "true", "false", "EXAMPLE-PHONE-BLK", "Black", "128GB", "299.00", "", "10", "2", "true"
        });
        var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(content, "text/csv; charset=utf-8", "kstore-products-template.csv");
    }

    private static void AppendCsvRow(StringBuilder csv, IEnumerable<string?> values)
    {
        csv.AppendLine(string.Join(",", values.Select(value =>
            $"\"{(value ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal)}\"")));
    }

    private sealed class ProductImportDraft(Product product)
    {
        public Product Product { get; } = product;
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products.AsNoTracking()
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        var movements = await _dbContext.StockMovements.AsNoTracking()
            .Include(m => m.ProductVariant)
            .Where(m => m.ProductVariant!.ProductId == id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(25)
            .ToListAsync(cancellationToken);

        ViewData["Title"] = _t["adm.productDetails"];
        ViewData["Active"] = "Products";

        return View(new AdminProductDetailsViewModel
        {
            Product = product,
            RecentMovements = movements
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        ViewData["Title"] = _t["dashboard.newProduct"];
        ViewData["Active"] = "Products";

        var model = new ProductFormViewModel();
        await PopulateFormOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductFormViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = _t["dashboard.newProduct"];
        ViewData["Active"] = "Products";

        model.Variants = CleanVariantRows(model.Variants);

        if (model.Variants.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Variants), "Add at least one product variant.");
        }

        await ValidateProductAsync(model, cancellationToken);

        var duplicateConfirmation = $"{model.BrandId}:{model.Name.Trim().ToUpperInvariant()}";
        var similarProducts = string.IsNullOrWhiteSpace(model.Name)
            ? new List<SimilarProductMatch>()
            : await _dbContext.Products.AsNoTracking()
                .Where(p => p.BrandId == model.BrandId
                    && p.Name.ToLower() == model.Name.Trim().ToLower())
                .OrderBy(p => p.Name)
                .Take(5)
                .Select(p => new SimilarProductMatch
                {
                    Id = p.Id,
                    Name = p.Name
                })
                .ToListAsync(cancellationToken);

        if (similarProducts.Count > 0
            && !string.Equals(
                model.SimilarProductConfirmation,
                duplicateConfirmation,
                StringComparison.Ordinal))
        {
            model.SimilarProducts = similarProducts;
            model.SimilarProductConfirmation = duplicateConfirmation;
            await PopulateFormOptionsAsync(model, cancellationToken);
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            await PopulateFormOptionsAsync(model, cancellationToken);
            return View(model);
        }

        var product = new Product
        {
            Name = model.Name.Trim(),
            Slug = model.Slug!,
            Description = model.Description?.Trim(),
            BrandId = model.BrandId,
            CategoryId = model.CategoryId,
            IsActive = model.IsActive,
            IsFeatured = model.IsFeatured,
            CreatedAt = DateTime.UtcNow
        };

        if (model.MainImage is not null)
        {
            var url = await _imageService.SaveProductImageAsync(model.MainImage, cancellationToken);

            if (url is null)
            {
                ModelState.AddModelError(
                    nameof(model.MainImage),
                    _imageService.LastError ?? "The image could not be saved.");
                await PopulateFormOptionsAsync(model, cancellationToken);
                return View(model);
            }

            product.MainImageUrl = url;
        }

        var galleryCount = 0;

        if (model.GalleryImages is { Count: > 0 })
        {
            foreach (var file in model.GalleryImages
                         .Where(f => f.Length > 0)
                         .Take(ImageService.MaxGalleryImages))
            {
                var url = await _imageService.SaveProductImageAsync(file, cancellationToken);

                if (url is null)
                {
                    ModelState.AddModelError(
                        nameof(model.GalleryImages),
                        _imageService.LastError ?? "A gallery image could not be saved.");
                    await PopulateFormOptionsAsync(model, cancellationToken);
                    return View(model);
                }

                product.Images.Add(new ProductImage
                {
                    Url = url,
                    AltText = product.Name,
                    DisplayOrder = galleryCount++,
                    IsMain = false
                });
            }
        }

        // A product that only got gallery images still needs a catalogue picture.
        if (string.IsNullOrWhiteSpace(product.MainImageUrl) && product.Images.Count > 0)
        {
            product.MainImageUrl = product.Images
                .OrderBy(i => i.DisplayOrder)
                .Select(i => i.Url)
                .FirstOrDefault();
        }

        var uploadedVariantImages = new List<string>();
        foreach (var variant in model.Variants)
        {
            if (variant.ImageFile is { Length: > 0 })
            {
                var imageUrl = await _imageService.SaveProductImageAsync(variant.ImageFile, cancellationToken);
                if (imageUrl is null)
                {
                    ModelState.AddModelError($"Variants[{model.Variants.IndexOf(variant)}].ImageFile",
                        _imageService.LastError ?? "The variant image could not be saved.");
                    foreach (var uploadedUrl in uploadedVariantImages) _imageService.TryDelete(uploadedUrl);
                    await PopulateFormOptionsAsync(model, cancellationToken);
                    return View(model);
                }

                uploadedVariantImages.Add(imageUrl);
                variant.ImageUrl = imageUrl;
            }

            product.Variants.Add(new ProductVariant
            {
                SKU = variant.SKU,
                Color = variant.Color?.Trim(),
                Storage = variant.Storage?.Trim(),
                ImageUrl = variant.ImageUrl?.Trim(),
                Price = variant.Price,
                CompareAtPrice = variant.CompareAtPrice,
                StockQuantity = variant.StockQuantity,
                LowStockThreshold = Math.Max(0, variant.LowStockThreshold),
                IsActive = variant.IsActive,
                CreatedAt = DateTime.UtcNow
            });
        }

        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Record the initial stock for variants that start with quantity.
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        foreach (var variant in product.Variants.Where(v => v.StockQuantity > 0))
        {
            _dbContext.StockMovements.Add(new StockMovement
            {
                ProductVariantId = variant.Id,
                QuantityChange = variant.StockQuantity,
                StockAfter = variant.StockQuantity,
                Type = StockMovementType.Inbound,
                Reason = "Initial stock on product creation",
                PerformedByUserId = userId
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            "Create",
            nameof(Product),
            product.Id.ToString(),
            $"Product '{product.Name}' created with {product.Variants.Count} variant(s).",
            cancellationToken);

        TempData["Success"] = _t.Format("msg.productCreated", product.Name);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products.AsNoTracking()
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        ViewData["Title"] = _t["adm.editProduct"];
        ViewData["Active"] = "Products";

        var model = new ProductFormViewModel
        {
            Id = product.Id,
            Name = product.Name,
            Slug = product.Slug,
            Description = product.Description,
            BrandId = product.BrandId,
            CategoryId = product.CategoryId,
            IsActive = product.IsActive,
            IsFeatured = product.IsFeatured,
            MainImageUrl = product.MainImageUrl,
            ExistingImages = product.Images
                .OrderBy(i => i.DisplayOrder)
                .Select(i => new ProductImageInfo { Id = i.Id, Url = i.Url })
                .ToList(),
            Variants = product.Variants
                .OrderBy(v => v.Id)
                .Select(v => new VariantFormItem
                {
                    Id = v.Id,
                    SKU = v.SKU,
                    Color = v.Color,
                    Storage = v.Storage,
                    ImageUrl = v.ImageUrl,
                    Price = v.Price,
                    CompareAtPrice = v.CompareAtPrice,
                    StockQuantity = v.StockQuantity,
                    LowStockThreshold = v.LowStockThreshold,
                    IsActive = v.IsActive
                })
                .ToList()
        };

        if (model.Variants.Count == 0)
        {
            model.Variants.Add(new VariantFormItem());
        }

        await PopulateFormOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProductFormViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = _t["adm.editProduct"];
        ViewData["Active"] = "Products";

        if (id != model.Id)
        {
            return BadRequest();
        }

        var product = await _dbContext.Products
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        model.Variants = CleanVariantRows(model.Variants);

        if (model.Variants.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Variants), "Add at least one product variant.");
        }

        await ValidateProductAsync(model, cancellationToken);

        if (!ModelState.IsValid)
        {
            await ReloadExistingStateAsync(model, product, cancellationToken);
            await PopulateFormOptionsAsync(model, cancellationToken);
            return View(model);
        }

        product.Name = model.Name.Trim();
        product.Slug = model.Slug!;
        product.Description = model.Description?.Trim();
        product.BrandId = model.BrandId;
        product.CategoryId = model.CategoryId;
        product.IsActive = model.IsActive;
        product.IsFeatured = model.IsFeatured;
        product.UpdatedAt = DateTime.UtcNow;

        // ---- Main image -----------------------------------------------------
        if (model.RemoveMainImage && model.MainImage is null)
        {
            _imageService.TryDelete(product.MainImageUrl);
            product.MainImageUrl = null;
        }

        if (model.MainImage is not null)
        {
            var url = await _imageService.SaveProductImageAsync(model.MainImage, cancellationToken);

            if (url is null)
            {
                ModelState.AddModelError(
                    nameof(model.MainImage),
                    _imageService.LastError ?? "The image could not be saved.");
                await ReloadExistingStateAsync(model, product, cancellationToken);
                await PopulateFormOptionsAsync(model, cancellationToken);
                return View(model);
            }

            _imageService.TryDelete(product.MainImageUrl);
            product.MainImageUrl = url;
        }

        // ---- Variants -------------------------------------------------------
        var postedIds = model.Variants
            .Where(v => v.Id > 0)
            .Select(v => v.Id)
            .ToHashSet();

        // Removing a variant that is referenced by orders, carts or stock history
        // would destroy or invalidate history, so it is deactivated instead.
        foreach (var existing in product.Variants.ToList())
        {
            if (postedIds.Contains(existing.Id))
            {
                continue;
            }

            var isReferenced =
                await _dbContext.OrderItems.AnyAsync(i => i.ProductVariantId == existing.Id, cancellationToken)
                || await _dbContext.CartItems.AnyAsync(c => c.ProductVariantId == existing.Id, cancellationToken)
                || await _dbContext.StockMovements.AnyAsync(m => m.ProductVariantId == existing.Id, cancellationToken);

            if (isReferenced)
            {
                existing.IsActive = false;
            }
            else
            {
                _dbContext.ProductVariants.Remove(existing);
            }
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var uploadedVariantImages = new List<string>();
        var replacedVariantImages = new List<string>();

        foreach (var posted in model.Variants.Where(v => v.Id > 0))
        {
            var existing = product.Variants.FirstOrDefault(v => v.Id == posted.Id);

            if (existing is null)
            {
                ModelState.AddModelError(nameof(model.Variants), "A variant does not belong to this product.");
                await ReloadExistingStateAsync(model, product, cancellationToken);
                await PopulateFormOptionsAsync(model, cancellationToken);
                return View(model);
            }

            var stockBefore = existing.StockQuantity;

            if (posted.ImageFile is { Length: > 0 })
            {
                var imageUrl = await _imageService.SaveProductImageAsync(posted.ImageFile, cancellationToken);
                if (imageUrl is null)
                {
                    ModelState.AddModelError($"Variants[{model.Variants.IndexOf(posted)}].ImageFile",
                        _imageService.LastError ?? "The variant image could not be saved.");
                    foreach (var uploadedUrl in uploadedVariantImages) _imageService.TryDelete(uploadedUrl);
                    await ReloadExistingStateAsync(model, product, cancellationToken);
                    await PopulateFormOptionsAsync(model, cancellationToken);
                    return View(model);
                }

                uploadedVariantImages.Add(imageUrl);
                if (!string.IsNullOrWhiteSpace(existing.ImageUrl)) replacedVariantImages.Add(existing.ImageUrl);
                posted.ImageUrl = imageUrl;
            }

            existing.SKU = posted.SKU;
            existing.Color = posted.Color?.Trim();
            existing.Storage = posted.Storage?.Trim();
            existing.ImageUrl = posted.ImageUrl?.Trim();
            existing.Price = posted.Price;
            existing.CompareAtPrice = posted.CompareAtPrice;
            existing.StockQuantity = posted.StockQuantity;
            existing.LowStockThreshold = Math.Max(0, posted.LowStockThreshold);
            existing.IsActive = posted.IsActive;

            if (stockBefore != posted.StockQuantity)
            {
                _dbContext.StockMovements.Add(new StockMovement
                {
                    ProductVariantId = existing.Id,
                    QuantityChange = posted.StockQuantity - stockBefore,
                    StockAfter = posted.StockQuantity,
                    Type = StockMovementType.Adjustment,
                    Reason = "Manual stock adjustment (admin edit)",
                    PerformedByUserId = userId
                });
            }
        }

        foreach (var posted in model.Variants.Where(v => v.Id == 0))
        {
            if (posted.ImageFile is { Length: > 0 })
            {
                var imageUrl = await _imageService.SaveProductImageAsync(posted.ImageFile, cancellationToken);
                if (imageUrl is null)
                {
                    ModelState.AddModelError($"Variants[{model.Variants.IndexOf(posted)}].ImageFile",
                        _imageService.LastError ?? "The variant image could not be saved.");
                    foreach (var uploadedUrl in uploadedVariantImages) _imageService.TryDelete(uploadedUrl);
                    await ReloadExistingStateAsync(model, product, cancellationToken);
                    await PopulateFormOptionsAsync(model, cancellationToken);
                    return View(model);
                }

                uploadedVariantImages.Add(imageUrl);
                posted.ImageUrl = imageUrl;
            }

            var variant = new ProductVariant
            {
                SKU = posted.SKU,
                Color = posted.Color?.Trim(),
                Storage = posted.Storage?.Trim(),
                ImageUrl = posted.ImageUrl?.Trim(),
                Price = posted.Price,
                CompareAtPrice = posted.CompareAtPrice,
                StockQuantity = posted.StockQuantity,
                LowStockThreshold = Math.Max(0, posted.LowStockThreshold),
                IsActive = posted.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            product.Variants.Add(variant);

            if (variant.StockQuantity > 0)
            {
                _dbContext.StockMovements.Add(new StockMovement
                {
                    ProductVariant = variant,
                    QuantityChange = variant.StockQuantity,
                    StockAfter = variant.StockQuantity,
                    Type = StockMovementType.Inbound,
                    Reason = "Initial stock for new variant",
                    PerformedByUserId = userId
                });
            }
        }

        // ---- Gallery images -------------------------------------------------
        if (model.GalleryImages is { Count: > 0 })
        {
            var remainingSlots = Math.Max(0, ImageService.MaxGalleryImages - product.Images.Count);

            foreach (var file in model.GalleryImages
                         .Where(f => f.Length > 0)
                         .Take(remainingSlots))
            {
                var url = await _imageService.SaveProductImageAsync(file, cancellationToken);

                if (url is null)
                {
                    foreach (var uploadedUrl in uploadedVariantImages) _imageService.TryDelete(uploadedUrl);
                    ModelState.AddModelError(
                        nameof(model.GalleryImages),
                        _imageService.LastError ?? "A gallery image could not be saved.");
                    await ReloadExistingStateAsync(model, product, cancellationToken);
                    await PopulateFormOptionsAsync(model, cancellationToken);
                    return View(model);
                }

                product.Images.Add(new ProductImage
                {
                    Url = url,
                    AltText = product.Name,
                    DisplayOrder = product.Images.Count
                });
            }
        }

        // Keep the catalogue picture in sync when only gallery images exist.
        if (string.IsNullOrWhiteSpace(product.MainImageUrl) && product.Images.Count > 0)
        {
            product.MainImageUrl = product.Images
                .OrderBy(i => i.DisplayOrder)
                .Select(i => i.Url)
                .FirstOrDefault();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var oldImageUrl in replacedVariantImages.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!await _dbContext.ProductVariants.AnyAsync(v => v.ImageUrl == oldImageUrl, cancellationToken))
            {
                _imageService.TryDelete(oldImageUrl);
            }
        }

        await _auditService.LogAsync(
            "Update",
            nameof(Product),
            product.Id.ToString(),
            $"Product '{product.Name}' updated ({product.Variants.Count} variant(s)).",
            cancellationToken);

        TempData["Success"] = _t.Format("msg.productUpdated", product.Name);
        return RedirectToAction(nameof(Edit), new { id = product.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        product.IsActive = !product.IsActive;
        product.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            product.IsActive ? "Activate" : "Deactivate",
            nameof(Product),
            product.Id.ToString(),
            $"Product '{product.Name}' active={product.IsActive}.",
            cancellationToken);

        TempData["Success"] = product.IsActive
            ? _t.Format("msg.productActivated", product.Name)
            : _t.Format("msg.productDeactivated", product.Name);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGalleryImage(int id, int imageId, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        var image = product.Images.FirstOrDefault(i => i.Id == imageId);

        if (image is not null)
        {
            _dbContext.ProductImages.Remove(image);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _imageService.TryDelete(image.Url);

            TempData["Success"] = _t["msg.galleryImageRemoved"];
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    private static List<VariantFormItem> CleanVariantRows(IEnumerable<VariantFormItem>? variants)
    {
        return (variants ?? Enumerable.Empty<VariantFormItem>())
            .Where(v => !string.IsNullOrWhiteSpace(v.SKU) || v.Price > 0)
            .ToList();
    }

    private async Task ValidateProductAsync(ProductFormViewModel model, CancellationToken cancellationToken)
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

        var brandExists = await _dbContext.Brands
            .AnyAsync(b => b.Id == model.BrandId, cancellationToken);

        if (!brandExists)
        {
            ModelState.AddModelError(nameof(model.BrandId), "Choose an existing brand.");
        }

        var categoryExists = await _dbContext.Categories
            .AnyAsync(c => c.Id == model.CategoryId, cancellationToken);

        if (!categoryExists)
        {
            ModelState.AddModelError(nameof(model.CategoryId), "Choose an existing category.");
        }

        var slugTaken = await _dbContext.Products
            .AnyAsync(p => p.Id != model.Id && p.Slug == model.Slug, cancellationToken);

        if (slugTaken)
        {
            ModelState.AddModelError(nameof(model.Slug), "Another product already uses this slug.");
        }

        for (var i = 0; i < model.Variants.Count; i++)
        {
            var variant = model.Variants[i];
            variant.SKU = (variant.SKU ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(variant.SKU))
            {
                ModelState.AddModelError($"Variants[{i}].SKU", "Enter a SKU.");
            }

            if (variant.CompareAtPrice.HasValue && variant.CompareAtPrice.Value < variant.Price)
            {
                ModelState.AddModelError(
                    $"Variants[{i}].CompareAtPrice",
                    "Compare-at price must be at least the selling price.");
            }
        }

        var duplicateSkus = model.Variants
            .Where(v => !string.IsNullOrWhiteSpace(v.SKU))
            .GroupBy(v => v.SKU, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var sku in duplicateSkus)
        {
            ModelState.AddModelError(
                nameof(model.Variants),
                _t.Format("msg.skuDuplicated", sku));
        }

        var skus = model.Variants
            .Select(v => v.SKU)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (skus.Count == 0)
        {
            return;
        }

        var takenSkus = await _dbContext.ProductVariants
            .Where(v => v.ProductId != model.Id && skus.Contains(v.SKU))
            .Select(v => v.SKU)
            .ToListAsync(cancellationToken);

        foreach (var sku in takenSkus)
        {
            ModelState.AddModelError(
                nameof(model.Variants),
                _t.Format("msg.skuTaken", sku));
        }
    }

    private async Task ReloadExistingStateAsync(
        ProductFormViewModel model,
        Product product,
        CancellationToken cancellationToken)
    {
        model.MainImageUrl = product.MainImageUrl;

        model.ExistingImages = await _dbContext.ProductImages.AsNoTracking()
            .Where(i => i.ProductId == product.Id)
            .OrderBy(i => i.DisplayOrder)
            .Select(i => new ProductImageInfo { Id = i.Id, Url = i.Url })
            .ToListAsync(cancellationToken);

        // Preserve the submitted variant rows so the user does not lose input.
        foreach (var variant in model.Variants.Where(v => v.Id > 0))
        {
            var exists = await _dbContext.ProductVariants
                .AnyAsync(v => v.Id == variant.Id && v.ProductId == product.Id, cancellationToken);

            if (!exists)
            {
                ModelState.AddModelError(nameof(model.Variants), "A variant does not belong to this product.");
            }
        }
    }

    private async Task PopulateFormOptionsAsync(ProductFormViewModel model, CancellationToken cancellationToken)
    {
        model.BrandOptions = await GetBrandOptionsAsync(cancellationToken);
        model.CategoryOptions = await GetCategoryOptionsAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<SelectListItem>> GetBrandOptionsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Brands.AsNoTracking()
            .OrderBy(b => b.Name)
            .Select(b => new SelectListItem(b.Name, b.Id.ToString()))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<SelectListItem>> GetCategoryOptionsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Categories.AsNoTracking()
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new SelectListItem(c.Name, c.Id.ToString()))
            .ToListAsync(cancellationToken);
    }
}
