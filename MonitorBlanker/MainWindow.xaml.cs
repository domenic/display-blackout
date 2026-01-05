using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace MonitorBlanker;

public sealed partial class MainWindow : Window
{
    public ObservableCollection<MonitorItem> Monitors { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        LoadMonitors();
        MonitorList.ItemsSource = Monitors;
    }

    private void LoadMonitors()
    {
        var displays = DisplayArea.FindAll();
        var primaryId = DisplayArea.Primary?.DisplayId.Value;

        // Use indexed loop - foreach throws InvalidCastException due to CsWinRT bug:
        // https://github.com/microsoft/WindowsAppSDK/issues/3484
        for (int i = 0; i < displays.Count; i++)
        {
            var display = displays[i];
            bool isPrimary = display.DisplayId.Value == primaryId;
            Monitors.Add(new MonitorItem
            {
                DisplayName = isPrimary ? $"{display.DisplayId.Value} (Primary)" : display.DisplayId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                IsSelected = !isPrimary,
                IsPrimary = isPrimary
            });
        }
    }
}

public sealed partial class MonitorItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public string DisplayName { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }

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
