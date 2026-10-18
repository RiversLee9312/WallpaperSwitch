namespace WallpaperSwitch.Services;

internal sealed class AsyncOperations
{
    private readonly Dictionary<string, int> _active = new();
    public event Action? Changed;
    public bool IsBusy { get { lock (_active) return _active.Count != 0; } }
    public bool Contains(string key) { lock (_active) return _active.ContainsKey(key); }

    // Called on the UI context; every success, cancellation and error restores state.
    public async Task RunAsync(string key, Func<Task> action, bool allowConcurrent = false)
    {
        lock (_active)
        {
            if (_active.ContainsKey(key) && !allowConcurrent) return;
            _active[key] = _active.GetValueOrDefault(key) + 1;
        }
        Changed?.Invoke();
        try { await action(); }
        finally
        {
            lock (_active) { if (--_active[key] == 0) _active.Remove(key); }
            Changed?.Invoke();
        }
    }
}
