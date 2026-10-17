using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WallpaperSwitch.Localization;

namespace WallpaperSwitch.Tests;

[TestClass]
public sealed class LocalizationFormatContractTests
{
    private static readonly Regex Placeholder = new(@"(?<!\{)\{(?<index>\d+)(?:,(?<alignment>-?\d+))?(?::(?<format>[^{}]+))?\}(?!\})");

    [TestMethod]
    public void EveryTranslationPreservesFormattingAndContainsTranslatedContent()
    {
        var baseline = ReadLanguage(LanguageCatalog.DefaultLanguage);
        foreach (var language in LanguageCatalog.Languages.Where(language => language.Tag != LanguageCatalog.SystemLanguage))
        {
            var translated = ReadLanguage(language.Tag);
            var changed = 0;
            foreach (var (key, original) in baseline)
            {
                Assert.IsTrue(translated.TryGetValue(key, out var value), $"Missing {language.Tag}/{key}");
                Assert.IsFalse(string.IsNullOrWhiteSpace(value), $"Empty {language.Tag}/{key}");
                if (value != original) changed++;

                var expectedFormats = Placeholder.Matches(original).Select(match => match.Value).Order().ToArray();
                var actualFormats = Placeholder.Matches(value!).Select(match => match.Value).Order().ToArray();
                CollectionAssert.AreEqual(expectedFormats, actualFormats,
                    $"Formatting contract changed for {language.Tag}/{key}");

                var matches = Placeholder.Matches(original);
                var length = matches.Count == 0 ? 0 : matches.Max(match => int.Parse(match.Groups["index"].Value, CultureInfo.InvariantCulture)) + 1;
                var arguments = Enumerable.Repeat<object?>("sample", length).ToArray();
                foreach (Match match in matches)
                {
                    var index = int.Parse(match.Groups["index"].Value, CultureInfo.InvariantCulture);
                    var format = match.Groups["format"].Value;
                    if (format.StartsWith("F", StringComparison.OrdinalIgnoreCase)) arguments[index] = 12.3456;
                    else if (format == "g") arguments[index] = new DateTimeOffset(2026, 10, 17, 9, 30, 0, TimeSpan.Zero);
                }
                _ = string.Format(LanguageCatalog.GetFormattingCulture(language.Tag), value!, arguments);
            }
            if (language.Tag != LanguageCatalog.DefaultLanguage)
                Assert.IsTrue(changed >= baseline.Count * 0.70,
                    $"{language.Tag} appears to copy the English baseline ({changed}/{baseline.Count} translated values).");
        }
    }

    private static Dictionary<string, string> ReadLanguage(string language) =>
        XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Strings", language, "Resources.resw"))
            .Root!.Elements("data")
            .ToDictionary(element => (string)element.Attribute("name")!, element => (string)element.Element("value")!);

    [TestMethod]
    public void StartupHelperResourcesCoverEveryAppLanguageAndPreserveFormats()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "HostResources");
        Dictionary<string, string> Read(string file) => XDocument.Load(Path.Combine(root, file))
            .Root!.Elements("data").ToDictionary(element => (string)element.Attribute("name")!, element => (string)element.Element("value")!);
        var baseline = Read("HostText.resx");
        foreach (var language in LanguageCatalog.Languages.Where(language => language.Tag is not (LanguageCatalog.SystemLanguage or LanguageCatalog.DefaultLanguage)))
        {
            var translated = Read($"HostText.{language.Tag}.resx");
            CollectionAssert.AreEquivalent(baseline.Keys.ToArray(), translated.Keys.ToArray(), language.Tag);
            foreach (var (key, value) in translated)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(value), $"{language.Tag}/{key}");
                CollectionAssert.AreEqual(Placeholder.Matches(baseline[key]).Select(match => match.Value).Order().ToArray(),
                    Placeholder.Matches(value).Select(match => match.Value).Order().ToArray(), $"{language.Tag}/{key}");
            }
        }
    }
}
