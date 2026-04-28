namespace FitTrack.Web.Services;

/// <summary>
/// Scoped service (one per Blazor circuit) that lets any page request a
/// confirmation dialog and await the user's response.
/// </summary>
public class ConfirmService
{
    private Func<string, string, Task>? _showCallback;
    private TaskCompletionSource<bool>? _pending;

    /// <summary>Called by <see cref="Shared.ConfirmDialog"/> on initialisation.</summary>
    internal void Register(Func<string, string, Task> callback) => _showCallback = callback;

    /// <summary>Called by <see cref="Shared.ConfirmDialog"/> on disposal.</summary>
    internal void Unregister() => _showCallback = null;

    /// <summary>
    /// Show a confirmation dialog and wait for the user to click Confirm or Cancel.
    /// Returns <c>true</c> if confirmed, <c>false</c> if cancelled.
    /// Falls back to <c>true</c> (no block) when no dialog is registered.
    /// </summary>
    public async Task<bool> ConfirmAsync(string message, string confirmLabel = "Delete")
    {
        if (_showCallback is null) return true;

        _pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await _showCallback(message, confirmLabel);
        return await _pending.Task;
    }

    internal void Complete(bool result)
    {
        var tcs = _pending;
        _pending = null;
        tcs?.TrySetResult(result);
    }
}

