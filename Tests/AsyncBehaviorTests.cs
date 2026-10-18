using System.Text.Json;
using WallpaperSwitch.Models;
using WallpaperSwitch.Services;

namespace WallpaperSwitch.Tests;

[TestClass]
public sealed class AsyncBehaviorTests
{
    [TestMethod]
    public async Task BusyOperationRejectsDuplicatesAndClearsAfterFailure()
    {
        var operations = new AsyncOperations();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = operations.RunAsync("appearance", () => completion.Task);
        Assert.IsTrue(operations.IsBusy);
        var duplicateRan = false;
        await operations.RunAsync("appearance", () => { duplicateRan = true; return Task.CompletedTask; });
        Assert.IsFalse(duplicateRan);
        var independentRan = false;
        await operations.RunAsync("location", () => { independentRan = true; return Task.CompletedTask; });
        Assert.IsTrue(independentRan);
        Assert.IsTrue(operations.Contains("appearance"));
        completion.SetException(new IOException("A controllable operation failed."));
        await Assert.ThrowsAsync<IOException>(() => first);
        Assert.IsFalse(operations.IsBusy);
        await operations.RunAsync("appearance", () => Task.CompletedTask);
        Assert.IsFalse(operations.IsBusy);
    }

    [TestMethod]
    public async Task OverlappingSavesKeepBusyUntilEverySaveFinishes()
    {
        var operations = new AsyncOperations();
        var one = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var two = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = operations.RunAsync("settings", () => one.Task, allowConcurrent: true);
        var second = operations.RunAsync("settings", () => two.Task, allowConcurrent: true);
        one.SetResult();
        await first;
        Assert.IsTrue(operations.IsBusy);
        two.SetCanceled();
        await Assert.ThrowsAsync<TaskCanceledException>(() => second);
        Assert.IsFalse(operations.IsBusy);
    }

    [TestMethod]
    public async Task TrayRecoveryRetriesEarlyShellFailuresAndCanBeCancelled()
    {
        var attempts = 0;
        var recovery = AsyncRetry.UntilReadyAsync(() => Task.FromResult(++attempts == 3),
            TimeSpan.FromMilliseconds(10), CancellationToken.None);
        Assert.IsFalse(recovery.IsCompleted, "Retry waiting must yield to the caller.");
        await recovery.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(3, attempts);

        using var cancellation = new CancellationTokenSource();
        var pending = AsyncRetry.UntilReadyAsync(() => Task.FromResult(false), TimeSpan.FromSeconds(30), cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAsync<TaskCanceledException>(() => pending);
    }

    [TestMethod]
    public async Task LockedSettingsSaveYieldsAndPreservesOriginalUntilAtomicReplacement()
    {
        var directory = Path.Combine(Path.GetTempPath(), "WallpaperSwitch.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var store = new SettingsFile(path);
            await store.SaveAsync(new AppSettings { Language = "en-US" });
            var updated = new AppSettings { Language = "de-DE" };
            updated.Dawn.WallpaperPath = "original-snapshot.jpg";
            Task save;
            using (var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                save = store.SaveAsync(updated);
                updated.Language = "fr-FR";
                updated.Dawn.WallpaperPath = "later-edit.jpg";
                await Task.Delay(200);
                if (save.IsFaulted) await save;
                Assert.IsFalse(save.IsCompleted, "The operation should be waiting without blocking the caller.");
                Assert.AreEqual("en-US", JsonDocument.Parse(await File.ReadAllTextAsync(path)).RootElement.GetProperty("language").GetString());
            }
            await save.WaitAsync(TimeSpan.FromSeconds(5));
            var loaded = await store.LoadAsync();
            Assert.AreEqual("de-DE", loaded.Language);
            Assert.AreEqual("original-snapshot.jpg", loaded.Dawn.WallpaperPath);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    public async Task RapidSettingsChangesPersistTheNewestCompleteSnapshot()
    {
        var directory = Path.Combine(Path.GetTempPath(), "WallpaperSwitch.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var store = new SettingsFile(Path.Combine(directory, "settings.json"));
            var writes = Enumerable.Range(0, 40).Select(index => store.SaveAsync(new AppSettings
            {
                Language = index.ToString(),
                Latitude = index,
                Dawn = new PeriodSettings { WallpaperPath = index.ToString() }
            })).ToArray();
            await Task.WhenAll(writes);
            var settings = await store.LoadAsync();
            Assert.AreEqual("39", settings.Language);
            Assert.AreEqual(39d, settings.Latitude);
            Assert.AreEqual("39", settings.Dawn.WallpaperPath);
            Assert.AreEqual(0, Directory.GetFiles(directory, "*.tmp").Length);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
