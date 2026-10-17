using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;
using WallpaperSwitch.Localization;
using WallpaperSwitch.Startup;

namespace WallpaperSwitch.Services;

internal sealed class StartupService : IStartupBackend
{
    private readonly string _applicationPath = Environment.ProcessPath
        ?? throw new InvalidOperationException(Localizer.GetString("Error.StartupPathMissing"));

    public bool StandardEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupConfiguration.RunKey);
            return key?.GetValue(StartupConfiguration.RunValueName) is string value
                && value.Equals(StartupConfiguration.CommandLine(_applicationPath), StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool HighPriorityEnabled => StartupConfiguration.IsRegistered(StartupConfiguration.CurrentSid, _applicationPath);
    public StartupMode Mode => HighPriorityEnabled ? StartupMode.HighPriority
        : StandardEnabled ? StartupMode.Standard : StartupMode.Disabled;

    public bool ServiceRunning
    {
        get
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{StartupConfiguration.ServiceName}");
            if (key?.GetValue("Start") is not int start || start != 2) return false;
            var processes = Process.GetProcessesByName("WallpaperSwitch.StartupHost");
            try { return processes.Any(process => process.SessionId == 0); }
            finally { foreach (var process in processes) process.Dispose(); }
        }
    }

    public Task SetModeAsync(StartupMode mode) => new StartupModeController(this).SetModeAsync(mode);

    public void SetStandard(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(StartupConfiguration.RunKey, writable: true);
        if (enabled)
            key.SetValue(StartupConfiguration.RunValueName, StartupConfiguration.CommandLine(_applicationPath));
        else
            key.DeleteValue(StartupConfiguration.RunValueName, throwOnMissingValue: false);
    }

    public async Task SetHighPriorityAsync(bool enabled)
    {
        var helper = Path.Combine(AppContext.BaseDirectory, "StartupService", StartupConfiguration.HostFileName);
        if (!File.Exists(helper))
            throw new FileNotFoundException(Localizer.GetString("Error.StartupHelperMissing"), helper);

        var start = new ProcessStartInfo(helper) { UseShellExecute = true, Verb = "runas" };
        start.ArgumentList.Add(enabled ? "--enable" : "--disable");
        start.ArgumentList.Add(StartupConfiguration.CurrentSid);
        start.ArgumentList.Add(_applicationPath);
        start.ArgumentList.Add("--language");
        start.ArgumentList.Add(SettingsStore.Current.Language);
        try
        {
            using var process = Process.Start(start) ?? throw new InvalidOperationException(Localizer.GetString("Error.StartupHelperStart"));
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
                throw new InvalidOperationException(Localizer.Format("Error.StartupHelperExit", process.ExitCode));
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new OperationCanceledException(Localizer.GetString("Startup.ElevationCancelled"), ex);
        }
    }
}
