using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WallpaperSwitch.Localization;

namespace WallpaperSwitch.Tests;

[TestClass]
public sealed class LocalizationResourceTests
{
    private static readonly Regex PlaceholderPattern = new(@"\{(?<index>\d+)(?:,[^{}]*)?(?::[^{}]*)?\}", RegexOptions.Compiled);

    [TestMethod]
    public void EverySupportedLanguageHasACompleteDistinctResourceFile()
    {
        var resourceRoot = Path.Combine(AppContext.BaseDirectory, "Strings");
        var supported = LanguageCatalog.Languages
            .Where(language => language.Tag != LanguageCatalog.SystemLanguage)
            .Select(language => language.Tag)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var actual = Directory.GetDirectories(resourceRoot)
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.IsTrue(supported.Count >= 20, "The app must provide at least 20 translated languages.");
        CollectionAssert.AreEquivalent(supported.ToArray(), actual.ToArray(), "The resource folders must match the language picker.");

        var english = ReadResources(Path.Combine(resourceRoot, LanguageCatalog.DefaultLanguage, "Resources.resw"));
        Assert.IsTrue(english.Count >= 100, "The English baseline should cover the full UI and app messages.");
        foreach (var language in supported)
        {
            var localized = ReadResources(Path.Combine(resourceRoot, language, "Resources.resw"));
            CollectionAssert.AreEquivalent(english.Keys.ToArray(), localized.Keys.ToArray(),
                $"{language} has missing or extra resource keys.");
            foreach (var key in english.Keys)
            {
                CollectionAssert.AreEquivalent(
                    GetPlaceholders(english[key]).ToArray(),
                    GetPlaceholders(localized[key]).ToArray(),
                    $"Placeholder indexes differ for {language}/{key}.");
            }
        }
    }

    [TestMethod]
    public void EveryXamlUidHasAnEnglishPropertyResource()
    {
        var resources = ReadResources(Path.Combine(AppContext.BaseDirectory, "Strings", LanguageCatalog.DefaultLanguage, "Resources.resw"));
        var xamlPath = Path.Combine(AppContext.BaseDirectory, "MainWindow.xaml");
        var xaml = XDocument.Load(xamlPath);
        XNamespace xamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
        foreach (var element in xaml.Descendants().Where(element => element.Attribute(xamlNamespace + "Uid") is not null))
        {
            var uid = (string)element.Attribute(xamlNamespace + "Uid")!;
            Assert.IsTrue(resources.Keys.Any(key => key.StartsWith(uid + ".", StringComparison.Ordinal)),
                $"XAML element x:Uid='{uid}' has no corresponding English resource.");
        }
    }

    [TestMethod]
    public void LanguageFallbackUsesParentAndEnglishResources()
    {
        CollectionAssert.AreEqual(new[] { "fr-FR", "fr", "en-US" }, LanguageCatalog.GetFallbackLanguages("fr-FR").ToArray());
        CollectionAssert.AreEqual(new[] { "en-US" }, LanguageCatalog.GetFallbackLanguages("system").ToArray());
        Assert.AreEqual("system", LanguageCatalog.NormalizePreference("unsupported-Latn"));
    }

    [TestMethod]
    public void SelectedCultureControlsRegionalFormattingAndRightToLeftLayout()
    {
        Assert.AreEqual("1,5", 1.5.ToString("F1", LanguageCatalog.GetFormattingCulture("de-DE")));
        Assert.IsTrue(LanguageCatalog.IsRightToLeft("ar-SA"));
        Assert.IsTrue(LanguageCatalog.IsRightToLeft("he-IL"));
        Assert.IsFalse(LanguageCatalog.IsRightToLeft("en-US"));
        Assert.IsTrue(LanguageCatalog.IsRightToLeft("system", CultureInfo.GetCultureInfo("ar-SA")));
    }

    private static SortedDictionary<string, string> ReadResources(string path)
    {
        var document = XDocument.Load(path);
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var element in document.Root!.Elements("data"))
        {
            var name = (string?)element.Attribute("name");
            var value = (string?)element.Element("value");
            Assert.IsFalse(string.IsNullOrWhiteSpace(name), $"Resource in {path} is missing its name.");
            Assert.IsNotNull(value, $"Resource {name} in {path} is missing its value.");
            Assert.IsTrue(result.TryAdd(name!, value!), $"Duplicate resource {name} in {path}.");
        }

        return result;
    }

    private static IEnumerable<string> GetPlaceholders(string value) =>
        PlaceholderPattern.Matches(value).Select(match => match.Groups["index"].Value).OrderBy(index => index, StringComparer.Ordinal);
}
