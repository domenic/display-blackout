using DisplayBlackout.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.ApplicationModel;
using Windows.Graphics;

namespace DisplayBlackout;

public sealed partial class MainWindow : WinUIEx.WindowEx
{
    private const string StartupTaskId = "DisplayBlackoutStartup";

    private readonly BlackoutService _blackoutService;
    private bool _isUpdatingStartupToggle;

    public ObservableCollection<MonitorItem> Monitors { get; } = [];

    public MainWindow(BlackoutService blackoutService)
    {
        _blackoutService = blackoutService;
        InitializeComponent();
        Title = App.ResourceLoader.GetString("AppDisplayName");
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "icon.ico"));
        AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;

        LoadMonitors();

        // Initialize toggle state and subscribe to external changes
        BlackoutToggle.IsOn = _blackoutService.IsBlackedOut;
        _blackoutService.BlackoutStateChanged += OnBlackoutStateChanged;

        // Initialize startup toggle state
        _ = InitializeStartupToggleAsync();
    }

    private async Task InitializeStartupToggleAsync()
    {
        try
        {
            var startupTask = await StartupTask.GetAsync(StartupTaskId);
            _isUpdatingStartupToggle = true;
            RunAtStartupToggle.IsOn = startupTask.State == StartupTaskState.Enabled;

            if (startupTask.State == StartupTaskState.DisabledByUser)
            {
                RunAtStartupToggle.IsEnabled = false;
                RunAtStartupCard.Description = App.ResourceLoader.GetString("RunAtStartupDisabledByUser");
            }
            else if (startupTask.State == StartupTaskState.DisabledByPolicy)
            {
                RunAtStartupToggle.IsEnabled = false;
                RunAtStartupCard.Description = App.ResourceLoader.GetString("RunAtStartupDisabledByPolicy");
            }

            _isUpdatingStartupToggle = false;
        }
        catch (FileNotFoundException)
        {
            // Startup task not available (e.g., manifest not properly configured) - disable the toggle
            RunAtStartupToggle.IsEnabled = false;
        }
    }

    private void OnBlackoutStateChanged(object? sender, BlackoutStateChangedEventArgs e)
    {
        // Update toggle when blackout state changes externally (hotkey, tray icon)
        DispatcherQueue.TryEnqueue(() =>
        {
            BlackoutToggle.IsOn = e.IsBlackedOut;
        });
    }

    private void BlackoutToggle_Toggled(object sender, RoutedEventArgs e)
    {
        // Only toggle if the state actually differs (avoids double-toggle from event handler)
        if (BlackoutToggle.IsOn != _blackoutService.IsBlackedOut)
        {
            _blackoutService.Toggle();
        }
    }

    private async void RunAtStartupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingStartupToggle)
        {
            return;
        }

        // Toggle is disabled if startup task wasn't found, so GetAsync should succeed here
        var startupTask = await StartupTask.GetAsync(StartupTaskId);
        if (RunAtStartupToggle.IsOn)
        {
            await startupTask.RequestEnableAsync();
        }
        else
        {
            startupTask.Disable();
        }

        // Refresh to show actual state (in case request was denied)
        _isUpdatingStartupToggle = true;
        RunAtStartupToggle.IsOn = startupTask.State == StartupTaskState.Enabled;
        _isUpdatingStartupToggle = false;
    }

    private void LoadMonitors()
    {
        var displays = DisplayArea.FindAll();
        if (displays.Count == 0) return;

        var primaryId = DisplayArea.Primary?.DisplayId.Value;

        // Copy to a list so we can sort it later, and calculate bounding boxes.
        //
        // Use an indexed loop - foreach throws InvalidCastException due to CsWinRT bug:
        // https://github.com/microsoft/WindowsAppSDK/issues/3484
        var displaysOrdered = new List<DisplayArea>(displays.Count);
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxMonitorHeight = 0;
        for (int i = 0; i < displays.Count; i++)
        {
            var display = displays[i];
            displaysOrdered.Add(display);

            var bounds = display.OuterBounds;
            minX = Math.Min(minX, bounds.X);
            minY = Math.Min(minY, bounds.Y);
            maxMonitorHeight = Math.Max(maxMonitorHeight, bounds.Height);
        }

        // Scale so tallest monitor is 160 DIPs (Viewbox will scale down further if needed)
        const double targetMonitorHeight = 160;
        double scale = targetMonitorHeight / maxMonitorHeight;

        // Sort by X then Y for consistent ordering (accessibility)
        displaysOrdered.Sort((a, b) =>
        {
            int xCompare = a.OuterBounds.X.CompareTo(b.OuterBounds.X);
            return xCompare != 0 ? xCompare : a.OuterBounds.Y.CompareTo(b.OuterBounds.Y);
        });

        // Calculate unique X positions for gap calculation
        const double gap = 2;
        var uniqueXPositions = displaysOrdered.Select(d => d.OuterBounds.X).Distinct().ToList();

        // Get selected monitor bounds (stable identifiers)
        var selectedBounds = _blackoutService.SelectedMonitorBounds;

        // Create monitor items
        for (int i = 0; i < displaysOrdered.Count; i++)
        {
            var display = displaysOrdered[i];
            var bounds = display.OuterBounds;
            bool isPrimary = display.DisplayId.Value == primaryId;
            var boundsKey = SettingsService.GetMonitorKey(bounds);

            // Add horizontal gaps between monitors based on how many X boundaries we've crossed
            int xGapCount = uniqueXPositions.Count(x => x < bounds.X);

            // Scale position and size relative to bounding box origin
            double scaledX = (bounds.X - minX) * scale + xGapCount * gap;
            double scaledY = (bounds.Y - minY) * scale;
            double scaledWidth = bounds.Width * scale;
            double scaledHeight = bounds.Height * scale;

            // If no selection saved, default to all non-primary
            bool isSelected = selectedBounds != null
                ? selectedBounds.Contains(boundsKey)
                : !isPrimary;

            Monitors.Add(new MonitorItem
            {
                IsPrimary = isPrimary,
                BoundsKey = boundsKey,
                IsSelected = isSelected,
                ScaledX = scaledX,
                ScaledY = scaledY,
                ScaledWidth = scaledWidth,
                ScaledHeight = scaledHeight,
                Bounds = bounds,
                MonitorIndex = i + 1,
                TotalMonitors = displaysOrdered.Count
            });
        }
    }

    private void MonitorToggleButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateBlackoutServiceSelection();
    }

    private void UpdateBlackoutServiceSelection()
    {
        var selectedBounds = new HashSet<string>();
        foreach (var monitor in Monitors)
        {
            if (monitor.IsSelected)
            {
                selectedBounds.Add(monitor.BoundsKey);
            }
        }
        _blackoutService.UpdateSelectedMonitors(selectedBounds);
    }

}

public sealed partial class MonitorItem : INotifyPropertyChanged
{
    public bool IsPrimary { get; set; }
    public string BoundsKey { get; set; } = string.Empty;

    public double ScaledX { get; set; }
    public double ScaledY { get; set; }
    public double ScaledWidth { get; set; }
    public double ScaledHeight { get; set; }
    public RectInt32 Bounds { get; set; }

    // Accessibility properties
    public int MonitorIndex { get; set; }
    public int TotalMonitors { get; set; }
    public string AccessibleName => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        App.ResourceLoader.GetString(IsPrimary ? "MonitorAccessibleNamePrimary" : "MonitorAccessibleName"),
        MonitorIndex,
        TotalMonitors);

    // Position as margin for Grid-based layout
    public Thickness PositionMargin => new(ScaledX, ScaledY, 0, 0);

    public bool IsSelected
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
