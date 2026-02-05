using Windows.Graphics;
using Windows.Storage;

namespace DisplayBlackout.Services;

public sealed class SettingsService
{
    private const string SelectedMonitorBoundsKey = "SelectedMonitorBounds";

    private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

    /// <summary>
    /// Creates a stable identifier for a monitor based on its bounds.
    /// Format: "X,Y,W,H" (e.g., "0,0,1920,1080")
    /// </summary>
    public static string GetMonitorKey(RectInt32 bounds)
        => $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}";

    public HashSet<string>? LoadSelectedMonitorBounds()
    {
        if (_localSettings.Values.TryGetValue(SelectedMonitorBoundsKey, out var value) && value is string str)
        {
            var bounds = new HashSet<string>();
            foreach (var part in str.Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                bounds.Add(part);
            }
            return bounds.Count > 0 ? bounds : null;
        }

        return null;
    }

    public void SaveSelectedMonitorBounds(HashSet<string>? monitorBounds)
    {
        if (monitorBounds is null || monitorBounds.Count == 0)
        {
            _localSettings.Values.Remove(SelectedMonitorBoundsKey);
        }
        else
        {
            _localSettings.Values[SelectedMonitorBoundsKey] = string.Join('|', monitorBounds);
        }
    }
}
