using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace MonitorBlanker;

public sealed partial class MainWindow : Window
{
    public ObservableCollection<MonitorItem> Monitors { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        LoadMonitors();
    }

    private void LoadMonitors()
    {
        var displays = DisplayArea.FindAll();
        if (displays.Count == 0) return;

        var primaryId = DisplayArea.Primary?.DisplayId.Value;

        // Reference values (in DIPs, matching Windows Display Settings at 150% DPI)
        // User measured 1496px physical at 150% = 997 DIPs
        const double containerWidth = 997;
        const double baseMonitorHeight = 160; // 240px physical / 1.5
        const double verticalPadding = 44;    // 66px physical / 1.5
        const double monitorGap = 1.5;        // ~2px physical / 1.5
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

            Monitors.Add(new MonitorItem
            {
                DisplayNumber = i + 1,
                IsPrimary = data.IsPrimary,
                IsSelected = !data.IsPrimary,
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

    private static Border CreateMonitorVisual(MonitorItem monitor)
    {
        var border = new Border
        {
            Width = monitor.ScaledWidth,
            Height = monitor.ScaledHeight,
            CornerRadius = new CornerRadius(4),
            Tag = monitor
        };

        var textBlock = new TextBlock
        {
            Text = monitor.DisplayNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        border.Child = textBlock;
        UpdateMonitorVisualState(border, monitor);

        border.PointerPressed += (s, e) =>
        {
            monitor.IsSelected = !monitor.IsSelected;
            UpdateMonitorVisualState(border, monitor);
        };

        monitor.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MonitorItem.IsSelected))
            {
                UpdateMonitorVisualState(border, monitor);
            }
        };

        return border;
    }

    private static void UpdateMonitorVisualState(Border border, MonitorItem monitor)
    {
        var textBlock = (TextBlock)border.Child;

        if (monitor.IsSelected)
        {
            // Selected for blanking - show as dark/will be blanked
            border.Background = new SolidColorBrush(Microsoft.UI.Colors.Black);
            border.BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0, 120, 212));
            border.BorderThickness = new Thickness(2);
            textBlock.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
        }
        else
        {
            // Not selected - show as normal/active
            border.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
            border.BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
            border.BorderThickness = new Thickness(1);
            textBlock.Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
        }
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

    public int DisplayNumber { get; set; }
    public bool IsPrimary { get; set; }

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
