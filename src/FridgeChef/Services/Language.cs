using System.Globalization;
using FridgeChef.Core.Resources;

namespace FridgeChef.Services;

// Chooses English or Khmer and switches text, fonts and line height together.
// A live switch = Save(code) then Router.Rebuild(), which rebuilds the visible screens in the new language.
public static class Language
{
    public const string PrefKey = "language";

    public static string Current { get; private set; } = "en";

    // Saved choice first; otherwise the phone's language.
    public static void ApplySaved() =>
        Apply(Preferences.Get(PrefKey, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "km" ? "km" : "en"));

    // Remember the choice and apply it now.
    public static void Save(string code)
    {
        Preferences.Set(PrefKey, code);
        Apply(code);
    }

    static void Apply(string code)
    {
        var km = code == "km";
        Current = km ? "km" : "en";
        var culture = new CultureInfo(km ? "km-KH" : "en-US");
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        AppResources.Culture = culture;

        // Styles use DynamicResource AppHeadingFont / AppBodyFont / AppBodyBoldFont / AppLineHeight, so this updates every label.
        // TryGetValue also searches Theme.xaml (a merged dictionary).
        var r = Application.Current!.Resources;
        var suffix = km ? "Km" : "En";
        foreach (var key in new[] { "HeadingFont", "BodyFont", "BodyBoldFont", "LineHeight" })
            if (r.TryGetValue(key + suffix, out var value)) r["App" + key] = value;
    }
}
