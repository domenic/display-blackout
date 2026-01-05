using H.NotifyIcon;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using MonitorBlanker.Services;

namespace MonitorBlanker;

public sealed partial class App : Application, IDisposable
{
    private TaskbarIcon? _trayIcon;
    private MainWindow? _settingsWindow;
    private Window? _hiddenWindow;
    private BlankingService? _blankingService;
    private HotkeyService? _hotkeyService;
    private GameModeService? _gameModeService;
    private bool _disposed;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _blankingService = new BlankingService();

        // Create a hidden window for hotkey messages
        _hiddenWindow = new Window { Title = "MonitorBlankerHidden" };

        _hotkeyService = new HotkeyService();
        _hotkeyService.HotkeyPressed += (_, _) => ToggleBlanking();
        _hotkeyService.Register(_hiddenWindow);

        _gameModeService = new GameModeService();
        _gameModeService.GameModeChanged += OnGameModeChanged;
        _gameModeService.StartMonitoring();

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Monitor Blanker - Click to open settings, double-click to toggle (Win+Shift+B)",
            IconSource = new GeneratedIconSource
            {
                Text = "MB",
                Foreground = new SolidColorBrush(Colors.White),
                Background = new SolidColorBrush(Colors.DarkSlateGray)
            }
        };

        _trayIcon.LeftClickCommand = new RelayCommand(ShowSettings);
        _trayIcon.DoubleClickCommand = new RelayCommand(ToggleBlanking);

        _trayIcon.ForceCreate();
    }

    private void OnGameModeChanged(object? sender, GameModeChangedEventArgs e)
    {
        // Game mode events come from Timer thread, marshal to UI thread
        if (_hiddenWindow?.DispatcherQueue.HasThreadAccess != true)
        {
            _hiddenWindow?.DispatcherQueue.TryEnqueue(() => OnGameModeChanged(sender, e));
            return;
        }

        if (e.IsInGameMode)
        {
            _blankingService?.Blank();
        }
        else
        {
            _blankingService?.Unblank();
        }
    }

    private void ShowSettings()
    {
        // Ensure we're on the UI thread
        if (_hiddenWindow?.DispatcherQueue.HasThreadAccess != true)
        {
            _hiddenWindow?.DispatcherQueue.TryEnqueue(ShowSettings);
            return;
        }

        if (_settingsWindow is null)
        {
            _settingsWindow = new MainWindow();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        _settingsWindow.Activate();
    }

    private void ToggleBlanking()
    {
        // Ensure we're on the UI thread
        if (_hiddenWindow?.DispatcherQueue.HasThreadAccess != true)
        {
            _hiddenWindow?.DispatcherQueue.TryEnqueue(ToggleBlanking);
            return;
        }

        _blankingService?.Toggle();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _gameModeService?.Dispose();
        _hotkeyService?.Dispose();
        _blankingService?.Dispose();
        _hiddenWindow?.Close();
        _trayIcon?.Dispose();
        GC.SuppressFinalize(this);
    }
}

internal sealed partial class RelayCommand(Action execute) : System.Windows.Input.ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
}
