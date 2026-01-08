using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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

        // Reference values (in DIPs, matching Windows Display Settings at 150% DPI)
        const double containerWidth = 998;
        const double baseMonitorHeight = 162;
        const double verticalPadding = 44;
        const double monitorGap = 2;
        const double minPaddingPercent = 0.075;

        // Build monitor list with visual dimensions based on aspect ratio
        var monitorList = new List<MonitorData>();

        // Use indexed loop - foreach throws InvalidCastException due to CsWinRT bug:
        // https://github.com/microsoft/WindowsAppSDK/issues/3484
        for (int i = 0; i < displays.Count; i++)
        {
            var display = displays[i];
            var bounds = display.OuterBounds;
            double aspectRatio = (double)bounds.Width / bounds.Height;

            monitorList.Add(new MonitorData
            {
                Display = display,
                AspectRatio = aspectRatio,
                BaseWidth = baseMonitorHeight * aspectRatio,
                BaseHeight = baseMonitorHeight,
                IsPrimary = display.DisplayId.Value == primaryId
            });
        }

        // Sort by physical X position (left to right arrangement)
        monitorList.Sort((a, b) => a.Display.OuterBounds.X.CompareTo(b.Display.OuterBounds.X));

        // Calculate total content width at base scale
        double totalBaseWidth = 0;
        for (int i = 0; i < monitorList.Count; i++)
        {
            totalBaseWidth += monitorList[i].BaseWidth;
            if (i < monitorList.Count - 1)
            {
                totalBaseWidth += monitorGap;
            }
        }

        // Calculate minimum padding and available width
        double minPadding = containerWidth * minPaddingPercent;
        double availableWidth = containerWidth - (2 * minPadding);

        // Determine scale factor
        double scale = 1.0;
        if (totalBaseWidth > availableWidth)
        {
            scale = availableWidth / totalBaseWidth;
        }

        // Calculate actual layout dimensions
        double layoutWidth = totalBaseWidth * scale;
        double layoutHeight = baseMonitorHeight * scale;

        // Calculate horizontal padding (center the content)
        double horizontalPadding = (containerWidth - layoutWidth) / 2;

        // Position and create monitor items
        double currentX = 0;
        for (int i = 0; i < monitorList.Count; i++)
        {
            var data = monitorList[i];

            double width = data.BaseWidth * scale;
            double height = data.BaseHeight * scale;

            // If no selection saved, default to all non-primary
            bool isSelected = selectedIds != null
                ? selectedIds.Contains(data.Display.DisplayId.Value)
                : !data.IsPrimary;

            Monitors.Add(new MonitorItem
            {
                IsPrimary = data.IsPrimary,
                DisplayId = data.Display.DisplayId.Value,
                IsSelected = isSelected,
                ScaledX = currentX,
                ScaledY = 0,
                ScaledWidth = width,
                ScaledHeight = height,
                Bounds = data.Display.OuterBounds
            });

            currentX += width;
            if (i < monitorList.Count - 1)
            {
                currentX += monitorGap * scale;
            }
        }

        // Set canvas size and margins
        MonitorCanvas.Width = layoutWidth;
        MonitorCanvas.Height = layoutHeight;
        MonitorCanvas.Margin = new Thickness(horizontalPadding, verticalPadding, horizontalPadding, verticalPadding);

        // Create visual elements
        foreach (var monitor in Monitors)
        {
            var border = CreateMonitorVisual(monitor);
            Canvas.SetLeft(border, monitor.ScaledX);
            Canvas.SetTop(border, monitor.ScaledY);
            MonitorCanvas.Children.Add(border);
        }
    }

    private Border CreateMonitorVisual(MonitorItem monitor)
    {
        var border = new Border
        {
            Width = monitor.ScaledWidth,
            Height = monitor.ScaledHeight,
            CornerRadius = new CornerRadius(4),
            Tag = monitor
        };

        bool isHovered = false;

        UpdateMonitorVisualState(border, monitor, isHovered);

        border.PointerEntered += (s, e) =>
        {
            isHovered = true;
            UpdateMonitorVisualState(border, monitor, isHovered);
        };

        border.PointerExited += (s, e) =>
        {
            isHovered = false;
            UpdateMonitorVisualState(border, monitor, isHovered);
        };

        border.PointerPressed += (s, e) =>
        {
            monitor.IsSelected = !monitor.IsSelected;
            UpdateMonitorVisualState(border, monitor, isHovered);
            UpdateBlankingServiceSelection();
        };

        monitor.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MonitorItem.IsSelected))
            {
                UpdateMonitorVisualState(border, monitor, isHovered);
            }
        };

        return border;
    }

    private static void UpdateMonitorVisualState(Border border, MonitorItem monitor, bool isHovered)
    {
        var resourceKey = (monitor.IsSelected, isHovered) switch
        {
            (true, true) => "MonitorSelectedHoverBackgroundBrush",
            (true, false) => "MonitorSelectedBackgroundBrush",
            (false, true) => "MonitorUnselectedHoverBackgroundBrush",
            (false, false) => "MonitorUnselectedBackgroundBrush"
        };
        border.Background = (Brush)Application.Current.Resources[resourceKey];
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

    private sealed class MonitorData
    {
        public required DisplayArea Display { get; init; }
        public double AspectRatio { get; init; }
        public double BaseWidth { get; init; }
        public double BaseHeight { get; init; }
        public bool IsPrimary { get; init; }
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
