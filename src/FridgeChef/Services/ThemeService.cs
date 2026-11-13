namespace FridgeChef.Services;

// Dark mode switch (Settings). Remembered on the phone; light by default (the Figma design is light).
public static class ThemeService
{
    const string Key = "dark_mode";
    public static bool IsDark => Preferences.Get(Key, false);

    public static void Apply() => Set(IsDark);

    public static void Set(bool dark)
    {
        Preferences.Set(Key, dark);
        Application.Current!.UserAppTheme = dark ? AppTheme.Dark : AppTheme.Light;
#if ANDROID
        // Status bar matches the page: white with dark icons, or near-black with light icons.
        if (Platform.CurrentActivity?.Window is { } window)
        {
            // Android 15+ draws the page behind the status bar, so only older phones need the colour.
            if (!OperatingSystem.IsAndroidVersionAtLeast(35))
                window.SetStatusBarColor(dark ? Android.Graphics.Color.ParseColor("#121212") : Android.Graphics.Color.White);
            AndroidX.Core.View.WindowCompat.GetInsetsController(window, window.DecorView)!.AppearanceLightStatusBars = !dark;
        }
#endif
    }
}
