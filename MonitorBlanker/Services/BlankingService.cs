using System.Globalization;
using Microsoft.UI.Windowing;

namespace MonitorBlanker.Services;

public sealed partial class BlankingService : IDisposable
{
    private readonly Dictionary<string, BlankWindow> _blankWindows = [];
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

        foreach (var display in displays)
        {
            if (display.DisplayId.Value == primaryId) continue;

            var window = new BlankWindow(display.OuterBounds);
            window.Activate();
            _blankWindows[display.DisplayId.Value.ToString(CultureInfo.InvariantCulture)] = window;
        }

        _isBlanked = true;
        BlankingStateChanged?.Invoke(this, new BlankingStateChangedEventArgs(true));
    }

    public void Unblank()
    {
        if (!_isBlanked) return;

        foreach (var window in _blankWindows.Values)
        {
            window.Close();
        }
        _blankWindows.Clear();

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
