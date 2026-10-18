using System.Text.Json;
using System.Text.Json.Serialization;
using WallpaperSwitch.Models;

namespace WallpaperSwitch.Services;

// Snapshots are captured before dispatch; slow disks never hold up the window.
internal sealed class SettingsFile(string path)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly SemaphoreSlim _writes = new(1, 1);
    private long _revision;
    private long _writtenRevision;

    public Task<AppSettings> LoadAsync() => Task.Run(async () =>
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                4096, FileOptions.Asynchronous);
            return await JsonSerializer.DeserializeAsync<AppSettings>(stream, Options) ?? new AppSettings();
        }
        catch (FileNotFoundException) { return new AppSettings(); }
        catch (DirectoryNotFoundException) { return new AppSettings(); }
        catch (JsonException) { return new AppSettings(); }
    });

    public Task SaveAsync(AppSettings settings)
    {
        var snapshot = settings.Clone();
        var revision = Interlocked.Increment(ref _revision);
        return Task.Run(async () =>
        {
            await _writes.WaitAsync();
            string? temporary = null;
            try
            {
                if (revision <= _writtenRevision) return;
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
                temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await JsonSerializer.SerializeAsync(stream, snapshot, Options);
                    await stream.FlushAsync();
                }

                // Antivirus, sync tools and readers may briefly deny atomic replacement.
                // Keep the previous valid settings until the replacement succeeds.
                for (var attempt = 0; ; attempt++)
                {
                    try { File.Move(temporary, path, overwrite: true); break; }
                    catch (IOException ex) when ((ex.HResult & 0xffff) is 32 or 33 && attempt < 99)
                    {
                        await Task.Delay(100);
                    }
                    catch (UnauthorizedAccessException) when (attempt < 99 && File.Exists(path)
                        && (File.GetAttributes(path) & FileAttributes.ReadOnly) == 0)
                    {
                        // MoveFileEx may report ACCESS_DENIED instead of SHARING_VIOLATION
                        // when a reader permits reading but denies replacing the target.
                        await Task.Delay(100);
                    }
                }
                _writtenRevision = revision;
            }
            finally
            {
                if (temporary is not null)
                {
                    try { File.Delete(temporary); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                _writes.Release();
            }
        });
    }
}
