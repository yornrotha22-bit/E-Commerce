using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using RothaShop.Localization;

namespace RothaShop.Controllers;

/// <summary>
/// Stores the visitor's UI language in a cookie and sends them straight back to
/// the page they were reading. The cookie is the only state: no database writes.
/// </summary>
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class LanguageController : Controller
{
    [HttpGet]
    public IActionResult Set(string? culture, string? returnUrl = null)
    {
        var normalized = SupportedCultures.Normalize(culture);

        Response.Cookies.Append(
            SupportedCultures.CookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(normalized, normalized)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                HttpOnly = false,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps
            });

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home", new { area = string.Empty });
    }
}
