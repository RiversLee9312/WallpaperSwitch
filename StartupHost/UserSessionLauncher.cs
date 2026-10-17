using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;
using WallpaperSwitch.Startup;

namespace WallpaperSwitch.StartupHost;

internal static class UserSessionLauncher
{
    internal static IEnumerable<int> GetSessions()
    {
        if (!WTSEnumerateSessions(0, 0, 1, out var buffer, out var count)) throw new Win32Exception();
        try
        {
            var size = Marshal.SizeOf<SessionInfo>();
            var result = new List<int>();
            for (var index = 0; index < count; index++)
            {
                var info = Marshal.PtrToStructure<SessionInfo>(buffer + index * size);
                if (info.SessionId != 0 && info.State is 0 or 1 or 4) result.Add(info.SessionId);
            }
            return result;
        }
        finally { WTSFreeMemory(buffer); }
    }

    internal static bool TryLaunch(int session)
    {
        if (!WTSQueryUserToken((uint)session, out var queriedToken)) return false;
        using var sessionToken = queriedToken;
        using var token = GetStandardToken(sessionToken);
        using var identity = new WindowsIdentity(token.DangerousGetHandle());
        if (identity.User is null || identity.IsSystem || identity.IsAnonymous) return true;
        var sid = identity.User.Value;
        var path = StartupConfiguration.GetApplicationPath(sid);
        if (path is null) return true;
        StartupConfiguration.Validate(sid, path);
        if (!File.Exists(path)) return false;

        var processes = Process.GetProcessesByName("WallpaperSwitch");
        try
        {
            if (processes.Any(process => IsSameApplication(process, session, path))) return true;
        }
        finally { foreach (var process in processes) process.Dispose(); }

        if (!CreateEnvironmentBlock(out var environment, token, false)) throw new Win32Exception();
        try
        {
            var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>(), Desktop = @"winsta0\default" };
            // An explicit application name and fixed arguments prevent executable path ambiguity.
            // Only the ordinary user's primary token is used, never the service's SYSTEM token.
            if (!CreateProcessAsUser(token, path, new StringBuilder(StartupConfiguration.CommandLine(path)),
                    0, 0, false, 0x00000400 | 0x00000020, environment,
                    Path.GetDirectoryName(path)!, ref startup, out var process)) throw new Win32Exception();
            try
            {
                // Retry an early bootstrap failure, while treating a clean single-instance
                // handoff as success. This runs on a worker, outside the SCM callback.
                if (WaitForSingleObject(process.Process, 5000) == 0)
                    return GetExitCodeProcess(process.Process, out var exitCode) && exitCode == 0;
                return true;
            }
            finally { CloseHandle(process.Thread); CloseHandle(process.Process); }
        }
        finally { DestroyEnvironmentBlock(environment); }
    }

    private static bool IsSameApplication(Process process, int session, string path)
    {
        try { return process.SessionId == session && string.Equals(process.MainModule?.FileName, path, StringComparison.OrdinalIgnoreCase); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { return false; }
    }

    private static SafeAccessTokenHandle GetStandardToken(SafeAccessTokenHandle original)
    {
        if (!GetTokenInformation(original, 18, out var elevation, sizeof(int), out _)) throw new Win32Exception();
        if (elevation == 2) // TokenElevationTypeFull: use its limited linked token.
        {
            if (!GetLinkedToken(original, 19, out var linked, IntPtr.Size, out _)) throw new Win32Exception();
            return new SafeAccessTokenHandle(linked);
        }
        if (!DuplicateTokenEx(original, 0x02000000, 0, 2, 1, out var duplicate)) throw new Win32Exception();
        return duplicate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SessionInfo { public int SessionId; public nint Station; public int State; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size;
        public string? Reserved;
        public string? Desktop;
        public string? Title;
        public int X, Y, XSize, YSize, XChars, YChars, FillAttribute, Flags;
        public short ShowWindow, ReservedSize;
        public nint ReservedPointer, StdInput, StdOutput, StdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInfo { public nint Process, Thread; public int ProcessId, ThreadId; }

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(uint session, out SafeAccessTokenHandle token);
    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSEnumerateSessions(nint server, int reserved, int version, out nint sessions, out int count);
    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(nint memory);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(SafeAccessTokenHandle existing, uint access, nint attributes,
        int impersonationLevel, int type, out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int information, out int value, int length, out int returned);
    [DllImport("advapi32.dll", EntryPoint = "GetTokenInformation", SetLastError = true)]
    private static extern bool GetLinkedToken(SafeAccessTokenHandle token, int information, out nint value, int length, out int returned);
    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool CreateEnvironmentBlock(out nint environment, SafeAccessTokenHandle token, bool inherit);
    [DllImport("userenv.dll")]
    private static extern bool DestroyEnvironmentBlock(nint environment);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcessAsUser(SafeAccessTokenHandle token, string application, StringBuilder command,
        nint processAttributes, nint threadAttributes, bool inherit, uint flags, nint environment,
        string directory, ref StartupInfo startup, out ProcessInfo process);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll")]
    private static extern uint WaitForSingleObject(nint handle, uint milliseconds);
    [DllImport("kernel32.dll")]
    private static extern bool GetExitCodeProcess(nint handle, out uint exitCode);
}
