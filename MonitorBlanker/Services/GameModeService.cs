using System.Runtime.InteropServices;

namespace MonitorBlanker.Services;

public sealed partial class GameModeService : IDisposable
{
    private const int QUNS_RUNNING_D3D_FULL_SCREEN = 5;

    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(2);
    private Timer? _timer;
    private bool _wasInGameMode;
    private bool _disposed;

    public event EventHandler<GameModeChangedEventArgs>? GameModeChanged;

    public static bool IsInGameMode => CheckGameMode();

    public void StartMonitoring()
    {
        _wasInGameMode = IsInGameMode;
        _timer = new Timer(CheckGameModeCallback, null, _pollInterval, _pollInterval);
    }

    public void StopMonitoring()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void CheckGameModeCallback(object? state)
    {
        bool isInGameMode = IsInGameMode;
        if (isInGameMode != _wasInGameMode)
        {
            _wasInGameMode = isInGameMode;
            GameModeChanged?.Invoke(this, new GameModeChangedEventArgs(isInGameMode));
        }
    }

    private static bool CheckGameMode()
    {
        return SHQueryUserNotificationState(out int state) == 0
            && state == QUNS_RUNNING_D3D_FULL_SCREEN;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopMonitoring();
    }

    [LibraryImport("shell32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int SHQueryUserNotificationState(out int pquns);
}

public sealed class GameModeChangedEventArgs(bool isInGameMode) : EventArgs
{
    public bool IsInGameMode { get; } = isInGameMode;
}
