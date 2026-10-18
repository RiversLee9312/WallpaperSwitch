using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WallpaperSwitch.Services;

namespace WallpaperSwitch.Tests;

[TestClass]
public sealed class LegalDocumentTests
{
    [TestMethod]
    public async Task BundledLicenseCatalogPreservesEveryDocumentAndRuntimeLicense()
    {
        var store = new LegalDocuments(AppContext.BaseDirectory);
        var catalog = await store.LoadCatalogAsync();
        Assert.IsTrue(catalog.Count > 30);
        foreach (var document in catalog)
        {
            var pages = await store.LoadPagesAsync(document);
            CollectionAssert.AllItemsAreNotNull(pages.ToArray());
            Assert.IsTrue(pages.All(page => page.Length <= 6000), document.Title);
            Assert.AreEqual(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, document.Path)),
                string.Concat(pages), $"Pagination must preserve the original text: {document.Title}");
        }
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Licenses", "manifest.json")));
        foreach (var package in manifest.RootElement.GetProperty("packages").EnumerateArray())
        {
            if (package.GetProperty("developmentOnly").GetBoolean()) continue;
            Assert.IsTrue(package.GetProperty("documents").EnumerateArray().Any(path =>
                path.GetString()!.Contains("license", StringComparison.OrdinalIgnoreCase)),
                $"Missing runtime license: {package.GetProperty("id").GetString()}");
        }
    }

    [TestMethod]
    public async Task ModifiedDocumentsAndEscapingPathsAreRejected()
    {
        var directory = Path.Combine(Path.GetTempPath(), "WallpaperSwitch.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var store = new LegalDocuments(directory);
            var original = Encoding.UTF8.GetBytes("Original license");
            var document = new LegalDocument("License", "LICENSE", Convert.ToHexString(SHA256.HashData(original)), "test");
            await File.WriteAllTextAsync(Path.Combine(directory, "LICENSE"), "Modified license");
            await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadPagesAsync(document));
            await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadPagesAsync(document with { Path = "../LICENSE" }));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
