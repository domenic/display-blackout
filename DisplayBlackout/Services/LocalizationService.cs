using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Globalization;
using Windows.System.UserProfile;

namespace DisplayBlackout.Services;

public sealed class LocalizationService
{
    private readonly ResourceManager _manager = new();
    private readonly ResourceContext _context;

    public string Language { get; private set; } = "en-US";

    public LocalizationService(string language)
    {
        _context = _manager.CreateResourceContext();
        SetLanguage(language);
    }

    public void SetLanguage(string language)
    {
        ApplicationLanguages.PrimaryLanguageOverride = language;
        // Read the OS preference rather than Languages, which includes our override.
        var systemLanguages = GlobalizationPreferences.Languages;
        Language = string.IsNullOrEmpty(language)
            ? systemLanguages.Count > 0 ? systemLanguages[0] : "en-US"
            : language;
        _context.QualifierValues["Language"] = Language;
    }

    public string GetString(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _manager.MainResourceMap.GetValue("Resources/" + key.Replace('.', '/'), _context).ValueAsString;
    }
}
