using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RothaShop.Data;
using RothaShop.ViewModels;

namespace RothaShop.ViewComponents;

/// <summary>
/// Renders store contact details from StoreSettings for the public footer.
/// </summary>
public class StoreInfoViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _dbContext;

    public StoreInfoViewComponent(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IViewComponentResult> InvokeAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.StoreSettings.AsNoTracking()
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        return View(new StoreInfoViewModel
        {
            StoreName = Read(settings, "StoreName") ?? "KShop",
            Currency = Read(settings, "Currency") ?? "USD",
            SupportPhone = Read(settings, "SupportPhone"),
            SupportEmail = Read(settings, "SupportEmail")
        });
    }

    private static string? Read(IReadOnlyDictionary<string, string?> settings, string key)
    {
        return settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }
}
