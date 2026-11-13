using FridgeChef.Core;
using FridgeChef.Core.Auth;
using FridgeChef.Core.Recipes;
using FridgeChef.Core.Resources;
using FridgeChef.Core.ViewModels;
using FridgeChef.Services;

namespace FridgeChef.Views;

// Simple on/off and navigation rows, so the logic stays here instead of a view model.
public partial class SettingsPage : ContentPage
{
	public SettingsPage(IAccountNavigator nav, LocalCache cache, AuthService auth, IToast toast)
	{
		InitializeComponent();
		VersionLabel.Text = $"Fridge Chef {string.Format(AppResources.Version, Format.Digits(AppInfo.Current.VersionString))}";
		LanguageRow.Value = Language.Current == "km" ? "ខ្មែរ" : "English";

		var dark = new Switch { IsToggled = ThemeService.IsDark, VerticalOptions = LayoutOptions.Center };
		dark.SetAppThemeColor(Switch.OnColorProperty, Color.FromArgb("#8C6D00"), Color.FromArgb("#D9B84A"));
		dark.Toggled += (_, e) => ThemeService.Set(e.Value);
		DarkRow.Accessory = dark;

		LanguageRow.Command = new Command(async () => await nav.OpenAsync("language"));
		PasswordRow.Command = new Command(async () => await nav.OpenAsync("change-password"));
		DeleteRow.Command = new Command(async () => await nav.OpenAsync("delete-account"));
		AboutRow.Command = new Command(async () => await nav.OpenAsync("about"));
		LogOutRow.Command = new Command(async () => await nav.OpenAsync("logout"));
		// Clears the phone copies of recipes, news and countries (saved recipes and the fridge are kept for offline use).
		CacheRow.Command = new Command(async () =>
		{
			await cache.RemoveAsync("home.trending", "home.popular", "news", "cuisines");
			toast.Show(ToastKind.Success, AppResources.CacheCleared);
		});

		// Google accounts have no password: hide the row instead of showing a screen that can't work.
		_ = HidePasswordRowForGoogleAsync(auth);
	}

	async Task HidePasswordRowForGoogleAsync(AuthService auth)
	{
		try { PasswordRow.IsVisible = (await auth.GetAccountAsync()).HasPassword; }
		catch (AuthException) { } // offline: leave it visible; the screen explains any problem
	}

	void OnBack(object? s, TappedEventArgs e) => _ = Navigation.PopAsync();
}
