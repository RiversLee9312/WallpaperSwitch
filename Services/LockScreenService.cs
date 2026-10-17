using System.Security.Cryptography;
using System.Text;
using WallpaperSwitch.Localization;
using Microsoft.Win32;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System.UserProfile;

namespace WallpaperSwitch.Services;

internal static class LockScreenService
{
    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WallpaperSwitch",
        "lockscreen-cache");

    public static async Task ApplyAsync(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(Localizer.GetString("Error.LockScreenFileMissing"), path);
        }

        DisableSpotlight();
        DeleteStaleSharedCache();

        var jpegPath = await EncodeJpegAsync(Path.GetFullPath(path));
        var uniquePath = CopyWithUniqueName(jpegPath);

        // Windows caches the first lock-screen image set by an unpackaged process.
        // Setting a throwaway JPEG first forces it to accept the real file afterwards.
        await SetImageFileAsync(await WriteBustJpegAsync(path));
        await Task.Delay(250);
        await SetImageFileAsync(uniquePath);

        CrashLog.Write("Lock screen set: " + uniquePath);
        CleanupCache(jpegPath, uniquePath);
    }

    private static async Task SetImageFileAsync(string fullPath)
    {
        var file = await StorageFile.GetFileFromPathAsync(fullPath);
        await LockScreen.SetImageFileAsync(file);
    }

    private static async Task<string> EncodeJpegAsync(string sourcePath)
    {
        Directory.CreateDirectory(CacheDirectory);

        var info = new FileInfo(sourcePath);
        var stamp = $"{sourcePath}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stamp)))[..16];
        var destination = Path.Combine(CacheDirectory, hash + ".jpg");
        if (File.Exists(destination) && new FileInfo(destination).Length > 0)
        {
            return destination;
        }

        var copied = Path.Combine(CacheDirectory, hash + info.Extension);
        File.Copy(sourcePath, copied, overwrite: true);

        var sourceFile = await StorageFile.GetFileFromPathAsync(copied);
        using (var input = await sourceFile.OpenAsync(FileAccessMode.Read))
        {
            var decoder = await BitmapDecoder.CreateAsync(input);
            var bitmap = SoftwareBitmap.Convert(
                await decoder.GetSoftwareBitmapAsync(),
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Ignore);

            var folder = await StorageFolder.GetFolderFromPathAsync(CacheDirectory);
            var jpegFile = await folder.CreateFileAsync(
                Path.GetFileName(destination),
                CreationCollisionOption.ReplaceExisting);
            using var output = await jpegFile.OpenAsync(FileAccessMode.ReadWrite);
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, output);
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync();
        }

        try
        {
            File.Delete(copied);
        }
        catch
        {
            // The transcoded JPEG is what matters.
        }

        return destination;
    }

    private static async Task<string> WriteBustJpegAsync(string sourcePath)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sourcePath));
        var pixels = new byte[16 * 16 * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = hash[0];
            pixels[i + 1] = hash[1];
            pixels[i + 2] = hash[2];
            pixels[i + 3] = 255;
        }

        var dest = Path.Combine(CacheDirectory, "bust-" + Guid.NewGuid().ToString("N") + ".jpg");
        var folder = await StorageFolder.GetFolderFromPathAsync(CacheDirectory);
        var file = await folder.CreateFileAsync(Path.GetFileName(dest), CreationCollisionOption.ReplaceExisting);
        using var output = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, output);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, 16, 16, 96, 96, pixels);
        await encoder.FlushAsync();
        return dest;
    }

    private static string CopyWithUniqueName(string jpegPath)
    {
        var destination = Path.Combine(CacheDirectory, Guid.NewGuid().ToString("N") + ".jpg");
        File.Copy(jpegPath, destination, overwrite: true);
        return destination;
    }

    private static void CleanupCache(params string[] keep)
    {
        try
        {
            var keepSet = new HashSet<string>(keep, StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(CacheDirectory))
            {
                if (keepSet.Contains(file))
                {
                    continue;
                }

                var name = Path.GetFileName(file);
                if (name.Length == 20 && name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Windows may still be reading the previous lock-screen file.
                }
            }
        }
        catch
        {
            // Cache cleanup is best-effort.
        }
    }

    private static void DeleteStaleSharedCache()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WallpaperSwitch");

        foreach (var name in new[] { "lockscreen.png", "lockscreen.jpg", "lockscreen.jpeg", "lockscreen.bmp" })
        {
            try
            {
                File.Delete(Path.Combine(directory, name));
            }
            catch
            {
                // The old shared cache file may still be locked by Windows.
            }
        }
    }

    private static void DisableSpotlight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                writable: true);
            if (key is null)
            {
                return;
            }

            key.SetValue("RotatingLockScreenEnabled", 0, RegistryValueKind.DWord);
            key.SetValue("RotatingLockScreenOverlayEnabled", 0, RegistryValueKind.DWord);
            key.SetValue("SubscribedContent-338387Enabled", 0, RegistryValueKind.DWord);
            key.SetValue("SubscribedContent-338388Enabled", 0, RegistryValueKind.DWord);
            key.SetValue("SubscribedContent-338389Enabled", 0, RegistryValueKind.DWord);
        }
        catch
        {
            // Best-effort: Spotlight may already be off, or the key may be missing.
        }
    }
}
