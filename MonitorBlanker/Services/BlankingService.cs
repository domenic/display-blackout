using System.Globalization;
using Microsoft.UI.Windowing;

namespace MonitorBlanker.Services;

public sealed partial class BlankingService : IDisposable
{
    private readonly Dictionary<string, BlankOverlay> _blankOverlays = [];
    private bool _isBlanked;
    private bool _disposed;

    public bool IsBlanked => _isBlanked;

    public event EventHandler<BlankingStateChangedEventArgs>? BlankingStateChanged;

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

        // Get all displays except primary
        var displays = DisplayArea.FindAll();
        var primaryId = DisplayArea.Primary?.DisplayId.Value;

        // Use indexed loop - foreach throws InvalidCastException due to CsWinRT bug:
        // https://github.com/microsoft/WindowsAppSDK/issues/3484
        for (int i = 0; i < displays.Count; i++)
        {
            var display = displays[i];
            if (display.DisplayId.Value == primaryId) continue;

            var overlay = new BlankOverlay(display.OuterBounds);
            _blankOverlays[display.DisplayId.Value.ToString(CultureInfo.InvariantCulture)] = overlay;
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
