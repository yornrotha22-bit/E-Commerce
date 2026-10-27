using System.Text.Json;
using RothaShop.Localization;

namespace RothaShop.Tests;

public class LocalizationTests
{
    private static readonly IReadOnlyDictionary<string, string> English =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["common.search"] = "Search",
            ["home.freeDeliveryOver"] = "Free delivery over ${0}",
            ["home.allProducts"] = "All {0} products"
        };

    private static readonly IReadOnlyDictionary<string, string> Khmer =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["common.search"] = "ស្វែងរក"
        };

    [Fact]
    public void Indexer_ReturnsTranslatedValue_ForCurrentCulture()
    {
        var translator = new Translator(Khmer, English);

        Assert.Equal("ស្វែងរក", translator["common.search"]);
    }

    [Fact]
    public void Indexer_FallsBackToEnglish_WhenKhmerKeyIsMissing()
    {
        var translator = new Translator(Khmer, English);

        Assert.Equal("Free delivery over ${0}", translator["home.freeDeliveryOver"]);
    }

    [Fact]
    public void Indexer_ReturnsKey_WhenNoDictionaryHasIt()
    {
        var translator = new Translator(Khmer, English);

        Assert.Equal("missing.key", translator["missing.key"]);
    }

    [Fact]
    public void Indexer_IgnoresBlankTranslations()
    {
        var current = new Dictionary<string, string>(StringComparer.Ordinal) { ["common.search"] = "   " };
        var translator = new Translator(current, English);

        Assert.Equal("Search", translator["common.search"]);
    }

    [Fact]
    public void Format_ReplacesPlaceholders()
    {
        var translator = new Translator(Khmer, English);

        Assert.Equal("All 8 products", translator.Format("home.allProducts", 8));
        Assert.Equal("ស្វែងរក", translator.Format("common.search"));
    }

    [Theory]
    [InlineData("km", "km")]
    [InlineData("km-KH", "km")]
    [InlineData("KM", "km")]
    [InlineData("en-US", "en")]
    [InlineData("fr", "en")]
    [InlineData("", "en")]
    [InlineData(null, "en")]
    public void Normalize_MapsAnyValueToASupportedCulture(string? input, string expected)
    {
        Assert.Equal(expected, SupportedCultures.Normalize(input));
    }

    [Fact]
    public void ShippedDictionaries_ContainTheSameKeys()
    {
        var directory = FindStringsDirectory();

        Assert.False(string.IsNullOrEmpty(directory), "Localization/Strings folder was not found.");

        using var englishStream = File.OpenRead(Path.Combine(directory!, "en.json"));
        using var khmerStream = File.OpenRead(Path.Combine(directory!, "km.json"));

        var english = JsonSerializer.Deserialize<Dictionary<string, string>>(englishStream)!;
        var khmer = JsonSerializer.Deserialize<Dictionary<string, string>>(khmerStream)!;

        var missing = english.Keys.Where(key => !khmer.ContainsKey(key)).ToList();

        Assert.True(
            missing.Count == 0,
            "Khmer dictionary is missing: " + string.Join(", ", missing));

        var extra = khmer.Keys.Where(key => !english.ContainsKey(key)).ToList();

        Assert.True(
            extra.Count == 0,
            "Khmer dictionary has keys that English does not: " + string.Join(", ", extra));
    }

    private static string? FindStringsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Localization", "Strings");

            if (File.Exists(Path.Combine(candidate, "en.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
