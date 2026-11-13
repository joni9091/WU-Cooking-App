using FridgeChef.Services;

namespace FridgeChef.Views.Auth;

public partial class LanguagePage : ContentPage
{
    readonly Router _router;

    public LanguagePage(Router router)
    {
        InitializeComponent();
        _router = router;
        var km = Language.Current == "km";
        Highlight(EnglishCard, EnglishCheck, !km);
        Highlight(KhmerCard, KhmerCheck, km);
    }

    // Selected card: pale yellow with olive border and a check (Figma 29).
    static void Highlight(Border card, Image check, bool selected)
    {
        card.BackgroundColor = selected ? ThemeColor("PaleYellow") : Colors.White;
        card.Stroke = ThemeColor(selected ? "Olive" : "PaleYellow");
        check.IsVisible = selected;
    }

    static Color ThemeColor(string key) =>
        Application.Current!.Resources.TryGetValue(key, out var v) ? (Color)v : Colors.Black;

    void OnEnglish(object? s, TappedEventArgs e) => Choose("en");
    void OnKhmer(object? s, TappedEventArgs e) => Choose("km");
    void OnBack(object? s, TappedEventArgs e) => _ = Navigation.PopAsync();

    void Choose(string code)
    {
        if (code == Language.Current) { _ = Navigation.PopAsync(); return; }
        Language.Save(code);
        _router.Rebuild(); // every screen is rebuilt in the new language at once
    }
}
