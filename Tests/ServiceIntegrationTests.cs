using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.SafeHandles;
using Microsoft.Win32;
using WallpaperSwitch.Startup;

namespace WallpaperSwitch.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ServiceIntegrationTests
{
    [TestMethod]
    [TestCategory("ServiceIntegration")]
    public void OptInServiceLaunchesInUserSessionAndUnregistersCleanly()
    {
        if (Environment.GetEnvironmentVariable("WALLPAPERSWITCH_SERVICE_TEST") != "1")
            Assert.Inconclusive("Set WALLPAPERSWITCH_SERVICE_TEST=1 to run the opt-in UAC/service integration test.");
        var app = StartupUiTests.ApplicationPath;
        var sid = StartupConfiguration.CurrentSid;
        Assert.IsNull(StartupConfiguration.GetApplicationPath(sid), "Run this test before manually enabling the service.");
        var helper = Environment.GetEnvironmentVariable("WALLPAPERSWITCH_TEST_HELPER")
            ?? Path.Combine(Path.GetDirectoryName(app)!, "StartupService", StartupConfiguration.HostFileName);
        Assert.IsTrue(File.Exists(helper));
        Process? launched = null;
        try
        {
            RunHelper(helper, "--enable", sid, app);
            Assert.IsTrue(StartupConfiguration.IsRegistered(sid, app));
            using (var controller = new ServiceController(StartupConfiguration.ServiceName))
                Assert.AreEqual(ServiceControllerStatus.Running, controller.Status);
            Assert.IsTrue(File.Exists(StartupConfiguration.InstalledHostPath));
            using (var serviceKey = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{StartupConfiguration.ServiceName}"))
            {
                Assert.IsNotNull(serviceKey);
                Assert.AreEqual(2, serviceKey.GetValue("Start"), "The opt-in service must start automatically at boot.");
                Assert.AreEqual($"\"{StartupConfiguration.InstalledHostPath}\" --service", serviceKey.GetValue("ImagePath"));
                Assert.AreEqual("LocalSystem", serviceKey.GetValue("ObjectName"));
            }

            StartupUiTests.WaitUntil(() =>
            {
                foreach (var candidate in Process.GetProcessesByName("WallpaperSwitch"))
                {
                    if (candidate.SessionId == Process.GetCurrentProcess().SessionId
                        && string.Equals(candidate.MainModule?.FileName, app, StringComparison.OrdinalIgnoreCase))
                    {
                        launched = candidate;
                        return true;
                    }
                    candidate.Dispose();
                }
                return false;
            });
            Assert.IsNotNull(launched);
            Assert.IsTrue(OpenProcessToken(launched.Handle, 0x0008, out var token));
            using (token)
            using (var identity = new WindowsIdentity(token.DangerousGetHandle()))
            {
                Assert.AreEqual(sid, identity.User?.Value);
                Assert.IsFalse(identity.IsSystem);
                Assert.IsTrue(GetTokenInformation(token, 20, out var elevated, sizeof(int), out _));
                Assert.AreEqual(0, elevated, "The service must launch the desktop application without elevation.");
            }
            Assert.AreEqual(ProcessPriorityClass.Normal, launched.PriorityClass);
        }
        finally
        {
            RunHelper(helper, "--disable", sid, app);
            if (launched is not null)
            {
                if (!launched.HasExited) launched.Kill();
                launched.Dispose();
            }
        }
        Assert.IsNull(StartupConfiguration.GetApplicationPath(sid));
        Assert.IsFalse(ServiceController.GetServices().Any(service =>
        {
            using (service) return service.ServiceName == StartupConfiguration.ServiceName;
        }));
    }

    private static void RunHelper(string helper, string command, string sid, string app)
    {
        var start = new ProcessStartInfo(helper) { UseShellExecute = true, Verb = "runas" };
        foreach (var argument in new[] { command, sid, app }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        Assert.IsTrue(process.WaitForExit(120000), "Service helper did not complete; check Windows UAC.");
        Assert.AreEqual(0, process.ExitCode);
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(nint process, uint access, out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int information, out int value, int length, out int returned);
}
