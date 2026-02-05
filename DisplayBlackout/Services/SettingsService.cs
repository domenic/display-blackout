using Windows.Graphics;
using Windows.Storage;

namespace DisplayBlackout.Services;

public sealed class SettingsService
{
    private const string SelectedMonitorBoundsKey = "SelectedMonitorBounds";
    private const string OpacityKey = "Opacity";
    private const int DefaultOpacity = 100;

    // Legacy key from before we switched to bounds-based identification
    private const string LegacySelectedMonitorIdsKey = "SelectedMonitorIds";

    private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

    public SettingsService()
    {
        // Clean up legacy setting if present
        _localSettings.Values.Remove(LegacySelectedMonitorIdsKey);
    }

    /// <summary>
    /// Creates a stable identifier for a monitor based on its bounds.
    /// Format: "X,Y,W,H" (e.g., "0,0,1920,1080")
    /// </summary>
    /// <remarks>
    /// We use bounds instead of display IDs because display IDs can change across reboots
    /// on some systems, causing users to lose their monitor selection.
    /// </remarks>
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

    public int LoadOpacity()
    {
        if (_localSettings.Values.TryGetValue(OpacityKey, out var value) && value is int opacity)
        {
            return Math.Clamp(opacity, 0, 100);
        }
        return DefaultOpacity;
    }

    public void SaveOpacity(int opacity)
    {
        _localSettings.Values[OpacityKey] = opacity;
    }
}
