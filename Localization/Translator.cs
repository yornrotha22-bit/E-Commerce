using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace RothaShop.Localization;

/// <summary>
/// Supported UI cultures. English is the fallback and the default for a first visit.
/// </summary>
public static class SupportedCultures
{
    public const string English = "en";

    public const string Khmer = "km";

    public const string CookieName = "RothaShop.Culture";

    public static readonly string[] All = { English, Khmer };

    /// <summary>Maps any incoming value to a supported two-letter culture.</summary>
    public static string Normalize(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return English;
        }

        var twoLetter = culture.Trim().Length >= 2
            ? culture.Trim()[..2].ToLowerInvariant()
            : English;

        return twoLetter == Khmer ? Khmer : English;
    }
}

/// <summary>
/// Read-only access to the UI strings of one language.
/// </summary>
public interface ITranslator
{
    /// <summary>Returns the translated string, or the key itself when it is missing.</summary>
    string this[string key] { get; }

    /// <summary>Returns the translated string with <see cref="string.Format(string, object?[])"/> applied.</summary>
    string Format(string key, params object?[] args);
}

/// <summary>
/// Dictionary-backed translator. Missing Khmer keys fall back to English so a
/// partly translated screen never shows raw keys.
/// </summary>
public sealed class Translator : ITranslator
{
    private readonly IReadOnlyDictionary<string, string> _current;
    private readonly IReadOnlyDictionary<string, string> _fallback;

    public Translator(IReadOnlyDictionary<string, string> current, IReadOnlyDictionary<string, string> fallback)
    {
        _current = current;
        _fallback = fallback;
    }

    public string this[string key]
    {
        get
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            if (_current.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            if (_fallback.TryGetValue(key, out var fallbackValue) && !string.IsNullOrWhiteSpace(fallbackValue))
            {
                return fallbackValue;
            }

            return key;
        }
    }

    public string Format(string key, params object?[] args)
    {
        var template = this[key];

        return args.Length == 0
            ? template
            : string.Format(CultureInfo.CurrentCulture, template, args);
    }
}

/// <summary>
/// Loads and caches the JSON dictionaries shipped in <c>Localization/Strings</c>.
/// Files are read once per process; editing a dictionary requires a restart.
/// </summary>
public sealed class TranslationStore
{
    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, string> Empty =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private readonly string _directory;

    public TranslationStore(IWebHostEnvironment environment)
    {
        _directory = Path.Combine(environment.ContentRootPath, "Localization", "Strings");
    }

    public IReadOnlyDictionary<string, string> Get(string culture)
    {
        var normalized = SupportedCultures.Normalize(culture);

        return Cache.GetOrAdd(normalized, key => Load(key));
    }

    private IReadOnlyDictionary<string, string> Load(string culture)
    {
        var path = Path.Combine(_directory, culture + ".json");

        if (!File.Exists(path))
        {
            return Empty;
        }

        try
        {
            using var stream = File.OpenRead(path);
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);

            return values is null || values.Count == 0
                ? Empty
                : new Dictionary<string, string>(values, StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            // A broken dictionary must not take the store down.
            return Empty;
        }
        catch (IOException)
        {
            return Empty;
        }
    }
}
