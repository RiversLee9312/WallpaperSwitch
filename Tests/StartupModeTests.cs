using Microsoft.VisualStudio.TestTools.UnitTesting;
using WallpaperSwitch.Models;
using WallpaperSwitch.Startup;

namespace WallpaperSwitch.Tests;

[TestClass]
public sealed class StartupModeTests
{
    [TestMethod]
    public void HighPriorityIsOptIn() => Assert.IsFalse(new AppSettings().HighPriorityStartup);

    [TestMethod]
    [DataRow(false, false, StartupMode.Disabled, false, false)]
    [DataRow(false, false, StartupMode.Standard, true, false)]
    [DataRow(true, false, StartupMode.HighPriority, false, true)]
    [DataRow(false, true, StartupMode.Standard, true, false)]
    [DataRow(false, true, StartupMode.Disabled, false, false)]
    [DataRow(true, false, StartupMode.Disabled, false, false)]
    [DataRow(false, true, StartupMode.HighPriority, false, true)]
    [DataRow(true, true, StartupMode.HighPriority, false, true)]
    public async Task ModeTransition(bool standard, bool high, StartupMode target, bool expectedStandard, bool expectedHigh)
    {
        var backend = new Backend { StandardEnabled = standard, HighPriorityEnabled = high };
        await new StartupModeController(backend).SetModeAsync(target);
        Assert.AreEqual(expectedStandard, backend.StandardEnabled);
        Assert.AreEqual(expectedHigh, backend.HighPriorityEnabled);
    }

    [TestMethod]
    public async Task CancelledElevationPreservesOrdinaryStartup()
    {
        var backend = new Backend { StandardEnabled = true, FailHigh = true };
        await Assert.ThrowsAsync<OperationCanceledException>(() => new StartupModeController(backend).SetModeAsync(StartupMode.HighPriority));
        Assert.IsTrue(backend.StandardEnabled);
        Assert.IsFalse(backend.HighPriorityEnabled);
    }

    [TestMethod]
    public async Task CancelledRemovalPreservesServiceStartup()
    {
        var backend = new Backend { HighPriorityEnabled = true, FailHigh = true };
        await Assert.ThrowsAsync<OperationCanceledException>(() => new StartupModeController(backend).SetModeAsync(StartupMode.Standard));
        Assert.IsFalse(backend.StandardEnabled);
        Assert.IsTrue(backend.HighPriorityEnabled);
    }

    [TestMethod]
    public async Task RegistryFailureRollsBackNewService()
    {
        var backend = new Backend { StandardEnabled = true, FailStandardOnce = true };
        await Assert.ThrowsAsync<IOException>(() => new StartupModeController(backend).SetModeAsync(StartupMode.HighPriority));
        Assert.IsTrue(backend.StandardEnabled);
        Assert.IsFalse(backend.HighPriorityEnabled);
    }

    [TestMethod]
    [DataRow(@"C:\Program Files\日夜切换\WallpaperSwitch.exe")]
    [DataRow(@"D:\Apps\WallpaperSwitch.exe")]
    public void ValidLocalExecutable(string path) => StartupConfiguration.Validate("S-1-5-21-1-2-3-1001", path);

    [TestMethod]
    [DataRow(@"\\server\share\WallpaperSwitch.exe")]
    [DataRow(@"WallpaperSwitch.exe")]
    [DataRow(@"C:\Apps\cmd.exe")]
    [DataRow("C:\\Apps\\a\"b\\WallpaperSwitch.exe")]
    [DataRow(@"C:\Apps:stream\WallpaperSwitch.exe")]
    public void InvalidExecutableRejected(string path) =>
        Assert.Throws<ArgumentException>(() => StartupConfiguration.Validate("S-1-5-21-1-2-3-1001", path));

    [TestMethod]
    public void CommandLineQuotesSpaces() => Assert.AreEqual(
        "\"C:\\Program Files\\WallpaperSwitch.exe\" --minimized",
        StartupConfiguration.CommandLine(@"C:\Program Files\WallpaperSwitch.exe"));

    private sealed class Backend : IStartupBackend
    {
        public bool StandardEnabled { get; set; }
        public bool HighPriorityEnabled { get; set; }
        public bool FailHigh { get; init; }
        public bool FailStandardOnce { get; set; }
        public void SetStandard(bool enabled)
        {
            if (FailStandardOnce) { FailStandardOnce = false; throw new IOException("Registry write failed"); }
            StandardEnabled = enabled;
        }
        public Task SetHighPriorityAsync(bool enabled)
        {
            if (FailHigh) throw new OperationCanceledException("UAC cancelled");
            HighPriorityEnabled = enabled;
            return Task.CompletedTask;
        }
    }
}
