using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace FridgeChef;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		// Figma: white status bar with dark icons, matching the page background.
		var window = Window!;
		window.SetStatusBarColor(Android.Graphics.Color.White);
		WindowCompat.GetInsetsController(window, window.DecorView).AppearanceLightStatusBars = true;
	}
}
