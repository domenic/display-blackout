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
    private BlankingService? _blankingService;
    private bool _disposed;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _blankingService = new BlankingService();

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Monitor Blanker - Click to open settings, double-click to toggle",
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

    private void ShowSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new MainWindow();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        _settingsWindow.Activate();
    }

    private void ToggleBlanking()
    {
        _blankingService?.Toggle();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _blankingService?.Dispose();
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
