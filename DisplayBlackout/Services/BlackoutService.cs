using Microsoft.UI.Windowing;

namespace DisplayBlackout.Services;

public sealed partial class BlackoutService : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly Dictionary<ulong, BlackoutOverlay> _blackoutOverlays = [];
    private HashSet<string>? _selectedMonitorBounds;
    private int _opacity;
    private bool _clickThrough;
    private bool _isBlackedOut;
    private bool _disposed;

    public BlackoutService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _selectedMonitorBounds = _settingsService.LoadSelectedMonitorBounds();
        _opacity = _settingsService.LoadOpacity();
        _clickThrough = _settingsService.LoadClickThrough();
    }

    public bool IsBlackedOut => _isBlackedOut;

    public event EventHandler<BlackoutStateChangedEventArgs>? BlackoutStateChanged;

    /// <summary>
    /// Updates which monitors should be blacked out using their bounds as stable identifiers.
    /// Null means default (all non-primary).
    /// </summary>
    public void UpdateSelectedMonitors(HashSet<string>? monitorBounds)
    {
        _selectedMonitorBounds = monitorBounds;
        _settingsService.SaveSelectedMonitorBounds(monitorBounds);

        if (_isBlackedOut)
        {
            RefreshOverlays();
        }
    }

    private void RefreshOverlays()
    {
        var displays = DisplayArea.FindAll();
        var primaryId = DisplayArea.Primary?.DisplayId.Value;
        var currentDisplayIds = new HashSet<ulong>();

        for (int i = 0; i < displays.Count; i++)
        {
            var display = displays[i];
            var displayId = display.DisplayId.Value;
            var bounds = display.OuterBounds;
            var boundsKey = SettingsService.GetMonitorKey(bounds);
            currentDisplayIds.Add(displayId);

            bool shouldBlackOut = _selectedMonitorBounds != null
                ? _selectedMonitorBounds.Contains(boundsKey)
                : displayId != primaryId;

            bool hasOverlay = _blackoutOverlays.ContainsKey(displayId);

            if (shouldBlackOut && !hasOverlay)
            {
                var overlay = new BlackoutOverlay(bounds, _opacity, _clickThrough);
                _blackoutOverlays[displayId] = overlay;
            }
            else if (!shouldBlackOut && hasOverlay)
            {
                _blackoutOverlays[displayId].Dispose();
                _blackoutOverlays.Remove(displayId);
            }
        }

        // Remove overlays for displays that no longer exist
        var toRemove = _blackoutOverlays.Keys.Where(id => !currentDisplayIds.Contains(id)).ToList();
        foreach (var id in toRemove)
        {
            _blackoutOverlays[id].Dispose();
            _blackoutOverlays.Remove(id);
        }
    }

    /// <summary>
    /// Gets the currently selected monitor bounds for UI initialization.
    /// </summary>
    public IReadOnlySet<string>? SelectedMonitorBounds => _selectedMonitorBounds;

    /// <summary>
    /// Gets the current opacity percentage (0-100).
    /// </summary>
    public int Opacity => _opacity;

    /// <summary>
    /// Updates the opacity of the blackout overlays.
    /// </summary>
    public void UpdateOpacity(int opacity)
    {
        _opacity = opacity;
        _settingsService.SaveOpacity(_opacity);

        // Update existing overlays
        foreach (var overlay in _blackoutOverlays.Values)
        {
            overlay.SetOpacity(_opacity);
        }
    }

    /// <summary>
    /// Gets whether click-through is enabled.
    /// </summary>
    public bool ClickThrough => _clickThrough;

    /// <summary>
    /// Updates whether the overlay is click-through.
    /// </summary>
    public void UpdateClickThrough(bool clickThrough)
    {
        _clickThrough = clickThrough;
        _settingsService.SaveClickThrough(_clickThrough);

        // Update existing overlays
        foreach (var overlay in _blackoutOverlays.Values)
        {
            overlay.SetClickThrough(_clickThrough);
        }
    }

    public void Toggle()
    {
        if (_isBlackedOut)
        {
            Restore();
        }
        else
        {
            BlackOut();
        }
    }

    public void BlackOut()
    {
        if (_isBlackedOut) return;

        var displays = DisplayArea.FindAll();
        var primaryId = DisplayArea.Primary?.DisplayId.Value;

        // Use indexed loop - foreach throws InvalidCastException due to CsWinRT bug:
        // https://github.com/microsoft/WindowsAppSDK/issues/3484
        for (int i = 0; i < displays.Count; i++)
        {
            var display = displays[i];
            var displayId = display.DisplayId.Value;
            var bounds = display.OuterBounds;
            var boundsKey = SettingsService.GetMonitorKey(bounds);

            // If selection is set, use it; otherwise default to all non-primary
            bool shouldBlackOut = _selectedMonitorBounds != null
                ? _selectedMonitorBounds.Contains(boundsKey)
                : displayId != primaryId;

            if (!shouldBlackOut) continue;

            var overlay = new BlackoutOverlay(bounds, _opacity, _clickThrough);
            _blackoutOverlays[displayId] = overlay;
        }

        _isBlackedOut = true;
        BlackoutStateChanged?.Invoke(this, new BlackoutStateChangedEventArgs(true));
    }

    public void Restore()
    {
        if (!_isBlackedOut) return;

        foreach (var overlay in _blackoutOverlays.Values)
        {
            overlay.Dispose();
        }
        _blackoutOverlays.Clear();

        _isBlackedOut = false;
        BlackoutStateChanged?.Invoke(this, new BlackoutStateChangedEventArgs(false));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Restore();
    }
}

public sealed class BlackoutStateChangedEventArgs(bool isBlackedOut) : EventArgs
{
    public bool IsBlackedOut { get; } = isBlackedOut;
}
