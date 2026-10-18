namespace WallpaperSwitch.Services;

internal static class AsyncRetry
{
    public static async Task UntilReadyAsync(Func<Task<bool>> tryOnce, TimeSpan delay, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await tryOnce()) return;
            await Task.Delay(delay, cancellationToken);
        }
    }
}
