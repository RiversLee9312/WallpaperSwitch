using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WallpaperSwitch.Services;

public sealed record LegalDocument(string Title, string Path, string Sha256, string Kind);

public sealed class LegalDocuments(string directory)
{
    private readonly string _root = System.IO.Path.GetFullPath(directory).TrimEnd(System.IO.Path.DirectorySeparatorChar)
        + System.IO.Path.DirectorySeparatorChar;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private sealed record Manifest(int SchemaVersion, LegalDocument[] Documents);

    public Task<IReadOnlyList<LegalDocument>> LoadCatalogAsync(CancellationToken cancellationToken = default) => Task.Run(async () =>
    {
        await using var file = File.OpenRead(ResolvePath("Licenses/manifest.json"));
        var manifest = await JsonSerializer.DeserializeAsync<Manifest>(file, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("The license catalog is empty.");
        if (manifest.SchemaVersion != 1 || manifest.Documents.Length == 0)
            throw new InvalidDataException("The license catalog format is unsupported.");
        foreach (var document in manifest.Documents) _ = ResolvePath(document.Path);
        return (IReadOnlyList<LegalDocument>)manifest.Documents;
    }, cancellationToken);

    public Task<IReadOnlyList<string>> LoadPagesAsync(LegalDocument document, CancellationToken cancellationToken = default) =>
        Task.Run(async () =>
        {
            var bytes = await File.ReadAllBytesAsync(ResolvePath(document.Path), cancellationToken).ConfigureAwait(false);
            var expected = Convert.FromHexString(document.Sha256);
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes), expected))
                throw new InvalidDataException("The legal document differs from the bundled license catalog.");
            using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            var pages = new List<string>();
            for (var start = 0; start < text.Length;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var length = Math.Min(6000, text.Length - start);
                if (start + length < text.Length)
                {
                    var newline = text.LastIndexOf('\n', start + length - 1, length);
                    if (newline >= start + length / 2) length = newline - start + 1;
                    else if (char.IsHighSurrogate(text[start + length - 1])) length--;
                }
                pages.Add(text.Substring(start, length));
                start += length;
            }
            if (pages.Count == 0) pages.Add(string.Empty);
            return (IReadOnlyList<string>)pages;
        }, cancellationToken);

    private string ResolvePath(string relativePath)
    {
        if (System.IO.Path.IsPathRooted(relativePath)) throw new InvalidDataException("A legal document path must be relative.");
        var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(_root, relativePath));
        if (!path.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A legal document path escapes the application directory.");
        return path;
    }
}
