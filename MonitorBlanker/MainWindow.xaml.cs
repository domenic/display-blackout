using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MonitorBlanker.Services;
using Windows.Graphics;

namespace MonitorBlanker;

public sealed partial class MainWindow : Window
{
    private readonly BlankingService _blankingService;

    public ObservableCollection<MonitorItem> Monitors { get; } = [];

    public MainWindow(BlankingService blankingService)
    {
        _blankingService = blankingService;
        InitializeComponent();
        AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;

        SetDefaultSize();
        LoadMonitors();
    }

    private void SetDefaultSize()
    {
        const int preferredWidth = 1600;
        const int preferredHeight = 1080;

        // Get the work area of the display where this window will appear
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;

        // Use preferred size, but don't exceed available space
        int width = Math.Min(preferredWidth, workArea.Width);
        int height = Math.Min(preferredHeight, workArea.Height);

        AppWindow.Resize(new SizeInt32(width, height));
    }

    private void LoadMonitors()
    {
        var displays = DisplayArea.FindAll();
        if (displays.Count == 0) return;

        var primaryId = DisplayArea.Primary?.DisplayId.Value;
        var selectedIds = _blankingService.SelectedMonitorIds;

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

        // Create monitor items
        for (int i = 0; i < displaysOrdered.Count; i++)
        {
            var display = displaysOrdered[i];
            var bounds = display.OuterBounds;
            bool isPrimary = display.DisplayId.Value == primaryId;

            // Scale position and size relative to bounding box origin
            double scaledX = (bounds.X - minX) * scale;
            double scaledY = (bounds.Y - minY) * scale;
            double scaledWidth = bounds.Width * scale;
            double scaledHeight = bounds.Height * scale;

            // If no selection saved, default to all non-primary
            bool isSelected = selectedIds != null
                ? selectedIds.Contains(display.DisplayId.Value)
                : !isPrimary;

            Monitors.Add(new MonitorItem
            {
                IsPrimary = isPrimary,
                DisplayId = display.DisplayId.Value,
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
        UpdateBlankingServiceSelection();
    }

    private void UpdateBlankingServiceSelection()
    {
        var selectedIds = new HashSet<ulong>();
        foreach (var monitor in Monitors)
        {
            if (monitor.IsSelected)
            {
                selectedIds.Add(monitor.DisplayId);
            }
        }
        _blankingService.UpdateSelectedMonitors(selectedIds);
    }

}

public sealed partial class MonitorItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public bool IsPrimary { get; set; }
    public ulong DisplayId { get; set; }

    public double ScaledX { get; set; }
    public double ScaledY { get; set; }
    public double ScaledWidth { get; set; }
    public double ScaledHeight { get; set; }
    public RectInt32 Bounds { get; set; }

    // Accessibility properties
    public int MonitorIndex { get; set; }
    public int TotalMonitors { get; set; }
    public string AccessibleName => IsPrimary
        ? $"Monitor {MonitorIndex} of {TotalMonitors}, primary"
        : $"Monitor {MonitorIndex} of {TotalMonitors}";

    // Position as margin for Grid-based layout
    public Thickness PositionMargin => new(ScaledX, ScaledY, 0, 0);

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
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
