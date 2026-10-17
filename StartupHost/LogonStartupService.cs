using System.ServiceProcess;

namespace WallpaperSwitch.StartupHost;

internal sealed class LogonStartupService : ServiceBase
{
    private readonly object _gate = new();
    private readonly Dictionary<int, CancellationTokenSource> _sessions = new();
    private readonly CancellationTokenSource _stop = new();

    public LogonStartupService()
    {
        ServiceName = Startup.StartupConfiguration.ServiceName;
        CanHandleSessionChangeEvent = true;
        CanShutdown = true;
        AutoLog = true;
    }

    protected override void OnStart(string[] args)
    {
        // SCM must receive a prompt response; token/profile readiness is retried in the worker.
        _ = Task.Run(() =>
        {
            try { foreach (var session in UserSessionLauncher.GetSessions()) Queue(session); }
            catch (Exception ex) { Log(ex); }
        });
    }

    protected override void OnSessionChange(SessionChangeDescription change)
    {
        if (change.Reason is SessionChangeReason.SessionLogoff or SessionChangeReason.SessionLogon)
        {
            lock (_gate)
            {
                if (_sessions.Remove(change.SessionId, out var cancellation))
                {
                    cancellation.Cancel();
                    cancellation.Dispose();
                }
            }
            if (change.Reason == SessionChangeReason.SessionLogon) Queue(change.SessionId);
        }
        else if (change.Reason is SessionChangeReason.ConsoleConnect or SessionChangeReason.RemoteConnect
                 or SessionChangeReason.SessionUnlock)
        {
            Queue(change.SessionId);
        }
    }

    private void Queue(int session)
    {
        if (session == 0) return;
        lock (_gate)
        {
            if (_stop.IsCancellationRequested || _sessions.ContainsKey(session)) return;
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            _sessions[session] = cancellation;
            _ = Task.Run(() => LaunchAsync(session, cancellation.Token));
        }
    }

    private async Task LaunchAsync(int session, CancellationToken token)
    {
        try
        {
            for (var attempt = 0; attempt < 60; attempt++)
            {
                token.ThrowIfCancellationRequested();
                try { if (UserSessionLauncher.TryLaunch(session)) return; }
                catch (Exception ex) { if (attempt is 0 or 59) Log(ex); }
                await Task.Delay(TimeSpan.FromSeconds(2), token);
            }
        }
        catch (OperationCanceledException) { }
        // Keep the session entry after success/exhaustion. Unlocking must not relaunch
        // an app the user deliberately exited; a new logon resets the entry.
    }

    protected override void OnStop()
    {
        lock (_gate) _stop.Cancel();
    }

    protected override void OnShutdown() => OnStop();

    private void Log(Exception exception)
    {
        try { EventLog.WriteEntry(exception.ToString(), System.Diagnostics.EventLogEntryType.Warning); }
        catch { /* A diagnostic failure must not stop logon handling. */ }
    }
}
