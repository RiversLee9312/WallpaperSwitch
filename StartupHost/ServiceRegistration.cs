using System.ComponentModel;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.ServiceProcess;
using Microsoft.Win32;
using WallpaperSwitch.Startup;

namespace WallpaperSwitch.StartupHost;

internal static class ServiceRegistration
{
    public static void Enable(string sid, string applicationPath)
    {
        if (!File.Exists(applicationPath)) throw new FileNotFoundException(HostText.Get("ApplicationMissing"), applicationPath);
        var previous = StartupConfiguration.GetApplicationPath(sid);
        var existed = Exists();
        try
        {
            InstallHost();
            var command = $"\"{StartupConfiguration.InstalledHostPath}\" --service";
            if (!existed)
                Sc("create", StartupConfiguration.ServiceName, "binPath=", command, "start=", "auto",
                    "obj=", "LocalSystem", "DisplayName=", HostText.Get("ServiceDisplayName"));
            else
                Sc("config", StartupConfiguration.ServiceName, "binPath=", command, "start=", "auto", "obj=", "LocalSystem");
            Sc("description", StartupConfiguration.ServiceName, HostText.Get("ServiceDescription"));
            Sc("failure", StartupConfiguration.ServiceName, "reset=", "86400", "actions=", "restart/5000/restart/15000/none/0");
            WriteUser(sid, applicationPath);
            Start();
        }
        catch
        {
            WriteUser(sid, previous);
            if (!existed && Exists()) { Stop(); Sc("delete", StartupConfiguration.ServiceName); }
            else if (existed) Start();
            throw;
        }
    }

    public static void Disable(string sid, string applicationPath)
    {
        // An older installation must not unregister a newer installation for the same user.
        if (!StartupConfiguration.IsRegistered(sid, applicationPath)) return;
        WriteUser(sid, null);
        try
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var users = root.OpenSubKey(StartupConfiguration.UsersKey);
            if (users?.SubKeyCount > 0) return;
            if (Exists()) { Stop(); Sc("delete", StartupConfiguration.ServiceName); }
        }
        catch
        {
            WriteUser(sid, applicationPath);
            if (Exists()) Start();
            throw;
        }
        // Removal of the inactive binary is best effort after SCM/configuration commit.
        // A locked file must not restore a registration for an already deleted service.
        try
        {
            var host = StartupConfiguration.InstalledHostPath;
            if (File.Exists(host) && (File.GetAttributes(host) & FileAttributes.ReparsePoint) == 0) File.Delete(host);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void WriteUser(string sid, string? path)
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var users = root.CreateSubKey(StartupConfiguration.UsersKey);
        if (path is null) users.DeleteSubKeyTree(sid, throwOnMissingSubKey: false);
        else
        {
            using var user = users.CreateSubKey(sid);
            user.SetValue("ApplicationPath", path, RegistryValueKind.String);
        }
    }

    private static void InstallHost()
    {
        var destination = StartupConfiguration.InstalledHostPath;
        var directory = Path.GetDirectoryName(destination)!;
        var parent = Directory.GetParent(directory)!.FullName;
        ProtectDirectory(parent);
        ProtectDirectory(directory);
        var source = Environment.ProcessPath!;
        if (File.Exists(destination))
        {
            using var input = File.OpenRead(source);
            using var installed = File.OpenRead(destination);
            if (SHA256.HashData(input).SequenceEqual(SHA256.HashData(installed))) return;
        }
        if (Exists()) Stop();
        var temporary = Path.Combine(directory, Guid.NewGuid() + ".tmp");
        try
        {
            File.Copy(source, temporary);
            var security = new FileSecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            security.SetOwner(administrators);
            security.AddAccessRule(new FileSystemAccessRule(administrators, FileSystemRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.ReadAndExecute, AccessControlType.Allow));
            new FileInfo(temporary).SetAccessControl(security);
            File.Move(temporary, destination, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void ProtectDirectory(string path)
    {
        var info = Directory.CreateDirectory(path);
        if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException(HostText.Get("UnsafeServiceDirectory"));
        var acl = new DirectorySecurity();
        acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        acl.SetOwner(administrators);
        foreach (var sid in new[] { administrators, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null) })
            acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Allow));
        info.SetAccessControl(acl);
    }

    internal static bool Exists()
    {
        using var service = new ServiceController(StartupConfiguration.ServiceName);
        try { _ = service.Status; return true; }
        catch (InvalidOperationException ex) when (ex.InnerException is Win32Exception { NativeErrorCode: 1060 }) { return false; }
    }

    private static void Stop()
    {
        using var service = new ServiceController(StartupConfiguration.ServiceName);
        if (service.Status == ServiceControllerStatus.Stopped) return;
        if (service.Status != ServiceControllerStatus.StopPending) service.Stop();
        service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
    }

    private static void Start()
    {
        using var service = new ServiceController(StartupConfiguration.ServiceName);
        if (service.Status == ServiceControllerStatus.Running) return;
        if (service.Status != ServiceControllerStatus.StartPending) service.Start();
        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
    }

    private static void Sc(params string[] arguments)
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "sc.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException(
            HostText.Format("ServiceManagementFailed", process.ExitCode, output, error));
    }
}
