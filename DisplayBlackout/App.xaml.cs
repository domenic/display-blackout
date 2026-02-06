using DisplayBlackout.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using WinUIEx;

namespace DisplayBlackout;

public sealed partial class App : Application, IDisposable
{
    private static readonly ResourceLoader s_resourceLoader = new();

    public static ResourceLoader ResourceLoader => s_resourceLoader;

    private TrayIcon? _trayIcon;
    private MainWindow? _settingsWindow;
    private bool _isShowingSettings;
    private SettingsService? _settingsService;
    private BlackoutService? _blackoutService;
    private string? _iconActivePath;
    private string? _iconInactivePath;
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

        _settingsService = new SettingsService();
        _blackoutService = new BlackoutService(_settingsService);

        SystemEventService.Instance.HotkeyPressed += (_, _) => ToggleBlackout();
        SystemEventService.Instance.DisplayChanged += (_, _) => OnDisplayChanged();

        _iconActivePath = Path.Combine(AppContext.BaseDirectory, "icon.ico");
        _iconInactivePath = Path.Combine(AppContext.BaseDirectory, "icon-inactive.ico");
        _trayIcon = new TrayIcon(1, _iconInactivePath, s_resourceLoader.GetString("TrayIconTooltip"));
        _trayIcon.Selected += (_, _) => ToggleBlackout();
        _trayIcon.LeftDoubleClick += (_, _) => ShowSettings();
        _trayIcon.ContextMenu += OnTrayContextMenu;
        _trayIcon.IsVisible = true;

        _blackoutService.BlackoutStateChanged += OnBlackoutStateChanged;

        if (openSettings)
        {
            ShowSettings();
        }
    }

    private void ShowSettings()
    {
        // Guard against rapid calls (e.g., double-clicking tray icon) that could create
        // multiple windows if a second call arrives while the constructor is still running.
        if (_isShowingSettings) return;

        if (_settingsWindow is null)
        {
            _isShowingSettings = true;
            _settingsWindow = new MainWindow(_blackoutService!);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _isShowingSettings = false;
        }

        _settingsWindow.Activate();
    }

    private void ToggleBlackout()
    {
        _blackoutService?.Toggle();
    }

    private void OnDisplayChanged()
    {
        // Turn off blackout - overlay positions are now invalid
        if (_blackoutService?.IsBlackedOut == true)
        {
            _blackoutService.Restore();
        }

        // Rebuild settings window monitor list if open
        _settingsWindow?.RebuildMonitorList();
    }

    private void OnBlackoutStateChanged(object? sender, BlackoutStateChangedEventArgs e)
    {
        var iconPath = e.IsBlackedOut ? _iconActivePath : _iconInactivePath;
        _trayIcon?.SetIcon(iconPath!);

        // Force tooltip refresh - the setter only updates if the value changes,
        // but changing the icon can cause the tooltip to break
        if (_trayIcon != null)
        {
            var tooltip = s_resourceLoader.GetString("TrayIconTooltip");
            _trayIcon.Tooltip = string.Empty;
            _trayIcon.Tooltip = tooltip;
        }
    }

    private void OnTrayContextMenu(TrayIcon sender, TrayIconEventArgs e)
    {
        var settingsItem = new MenuFlyoutItem { Text = s_resourceLoader.GetString("ContextMenuSettings") };
        settingsItem.Click += (_, _) => ShowSettings();

        var toggleItem = new MenuFlyoutItem { Text = s_resourceLoader.GetString("ContextMenuToggle") };
        toggleItem.Click += (_, _) => ToggleBlackout();

        var exitItem = new MenuFlyoutItem { Text = s_resourceLoader.GetString("ContextMenuExit") };
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
        SystemEventService.Instance.Dispose();
        _blackoutService?.Dispose();
        _trayIcon?.Dispose();
        GC.SuppressFinalize(this);
    }
}
