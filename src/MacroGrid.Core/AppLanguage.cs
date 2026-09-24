using System.Globalization;

namespace MacroGrid.Core;

/// <summary>
/// The language the app currently speaks ("tr" or "en"), for the few texts the server itself produces: the default words of a
/// boolean variable, the sample profile created on the first run. Until the preferences are known it follows the Windows
/// display language; the host then keeps it in step with the stored preference (<c>AppPreferences.Language</c>).
/// </summary>
public static class AppLanguage
{
    private static volatile string _current = SystemDefault();

    public static string Current
    {
        get => _current;
        set => _current = Normalize(value);
    }

    /// <summary>Turkish on a Turkish Windows, English on every other one.</summary>
    public static string SystemDefault() => Normalize(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    public static bool IsTurkish => _current == "tr";

    /// <summary>Picks the English or the Turkish text for the current language.</summary>
    public static string Pick(string english, string turkish) => IsTurkish ? turkish : english;

    private static string Normalize(string? language) =>
        string.Equals(language, "tr", StringComparison.OrdinalIgnoreCase) ? "tr" : "en";
}
