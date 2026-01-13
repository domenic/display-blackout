using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MonitorBlanker.Services;
using WinUIEx;

namespace MonitorBlanker;

public sealed partial class App : Application, IDisposable
{
    private TrayIcon? _trayIcon;
    private MainWindow? _settingsWindow;
    private Window? _hiddenWindow;
    private BlankingService? _blankingService;
    private HotkeyService? _hotkeyService;
    private bool _disposed;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        bool openSettings = Environment.GetCommandLineArgs()
            .Skip(1) // Skip executable path
            .Any(arg => arg.Equals("/OpenSettings", StringComparison.OrdinalIgnoreCase));

        _blankingService = new BlankingService();

        // Create a hidden window for hotkey messages
        _hiddenWindow = new Window { Title = "MonitorBlankerHidden" };

        _hotkeyService = new HotkeyService();
        _hotkeyService.HotkeyPressed += (_, _) => ToggleBlanking();
        _hotkeyService.Register(_hiddenWindow);

        var iconPath = Path.Combine(AppContext.BaseDirectory, "icon.ico");
        _trayIcon = new TrayIcon(1, iconPath, "Monitor Blanker - Click to open settings, double-click to toggle (Win+Shift+B)");
        _trayIcon.Selected += (_, _) => ShowSettings();
        _trayIcon.LeftDoubleClick += (_, _) => ToggleBlanking();
        _trayIcon.ContextMenu += OnTrayContextMenu;
        _trayIcon.IsVisible = true;

        if (openSettings)
        {
            ShowSettings();
        }
    }

    private void ShowSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new MainWindow(_blankingService!);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        _settingsWindow.Activate();
    }

    private void ToggleBlanking()
    {
        _blankingService?.Toggle();
    }

    private void OnTrayContextMenu(TrayIcon sender, TrayIconEventArgs e)
    {
        var settingsItem = new MenuFlyoutItem { Text = "Settings" };
        settingsItem.Click += (_, _) => ShowSettings();

        var toggleItem = new MenuFlyoutItem { Text = "Toggle" };
        toggleItem.Click += (_, _) => ToggleBlanking();

        var exitItem = new MenuFlyoutItem { Text = "Exit" };
        exitItem.Click += (_, _) => Environment.Exit(0);

        var flyout = new MenuFlyout();
        flyout.Items.Add(settingsItem);
        flyout.Items.Add(toggleItem);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(exitItem);

        e.Flyout = flyout;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _hotkeyService?.Dispose();
        _blankingService?.Dispose();
        _hiddenWindow?.Close();
        _trayIcon?.Dispose();
        GC.SuppressFinalize(this);
    }
}
