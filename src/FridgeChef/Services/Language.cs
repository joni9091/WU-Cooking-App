using System.Globalization;
using FridgeChef.Core.Resources;

namespace FridgeChef.Services;

// Chooses English or Khmer and switches text, fonts and line height together.
// The live switch (Welcome + Settings, later phase) will: save the choice, call Apply, then
// set Window.Page = new AppShell() so every page is rebuilt in the new language at once.
public static class Language
{
    public const string PrefKey = "language";

    // Saved choice first; otherwise the phone's language.
    public static void ApplySaved() =>
        Apply(Preferences.Get(PrefKey, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "km" ? "km" : "en"));

    public static void Apply(string code)
    {
        var km = code == "km";
        var culture = new CultureInfo(km ? "km-KH" : "en-US");
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        AppResources.Culture = culture;

        // Styles use DynamicResource AppHeadingFont / AppBodyFont / AppLineHeight, so this updates every label.
        // TryGetValue also searches Theme.xaml (a merged dictionary).
        var r = Application.Current!.Resources;
        var suffix = km ? "Km" : "En";
        foreach (var key in new[] { "HeadingFont", "BodyFont", "LineHeight" })
            if (r.TryGetValue(key + suffix, out var value)) r["App" + key] = value;
    }
}
