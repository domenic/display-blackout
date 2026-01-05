using Microsoft.UI.Windowing;

namespace MonitorBlanker.Services;

public sealed partial class BlankingService : IDisposable
{
    private readonly Dictionary<ulong, BlankOverlay> _blankOverlays = [];
    private HashSet<ulong>? _selectedMonitorIds;
    private bool _isBlanked;
    private bool _disposed;

    public bool IsBlanked => _isBlanked;

    public event EventHandler<BlankingStateChangedEventArgs>? BlankingStateChanged;

    /// <summary>
    /// Updates which monitors should be blanked. Null means default (all non-primary).
    /// </summary>
    public void UpdateSelectedMonitors(HashSet<ulong>? monitorIds)
    {
        _selectedMonitorIds = monitorIds;
    }

    /// <summary>
    /// Gets the currently selected monitor IDs for UI initialization.
    /// </summary>
    public IReadOnlySet<ulong>? SelectedMonitorIds => _selectedMonitorIds;

    public void Toggle()
    {
        if (_isBlanked)
        {
            Unblank();
        }
        else
        {
            Blank();
        }
    }

    public void Blank()
    {
        if (_isBlanked) return;

        var displays = DisplayArea.FindAll();
        var primaryId = DisplayArea.Primary?.DisplayId.Value;

        // Use indexed loop - foreach throws InvalidCastException due to CsWinRT bug:
        // https://github.com/microsoft/WindowsAppSDK/issues/3484
        for (int i = 0; i < displays.Count; i++)
        {
            var display = displays[i];
            var displayId = display.DisplayId.Value;

            // If selection is set, use it; otherwise default to all non-primary
            bool shouldBlank = _selectedMonitorIds != null
                ? _selectedMonitorIds.Contains(displayId)
                : displayId != primaryId;

            if (!shouldBlank) continue;

            var overlay = new BlankOverlay(display.OuterBounds);
            _blankOverlays[displayId] = overlay;
        }

        _isBlanked = true;
        BlankingStateChanged?.Invoke(this, new BlankingStateChangedEventArgs(true));
    }

    public void Unblank()
    {
        if (!_isBlanked) return;

        foreach (var overlay in _blankOverlays.Values)
        {
            overlay.Dispose();
        }
        _blankOverlays.Clear();

        _isBlanked = false;
        BlankingStateChanged?.Invoke(this, new BlankingStateChangedEventArgs(false));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Unblank();
    }
}

public sealed class BlankingStateChangedEventArgs(bool isBlanked) : EventArgs
{
    public bool IsBlanked { get; } = isBlanked;
}
