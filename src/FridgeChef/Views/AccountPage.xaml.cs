using FridgeChef.Core.Resources;
using FridgeChef.Services;

namespace FridgeChef.Views;

public partial class AccountPage : ContentPage
{
	readonly Router _router;

	public AccountPage(Router router)
	{
		InitializeComponent();
		_router = router;
	}

	// Language picker (Figma 29); choosing a language rebuilds the app in that language.
	async void OnLanguage(object? sender, EventArgs e) =>
		await Navigation.PushAsync(Handler!.MauiContext!.Services.GetRequiredService<Views.Auth.LanguagePage>());

	async void OnLogOut(object? sender, EventArgs e)
	{
		if (await DisplayAlertAsync(AppResources.LogOutTitle, AppResources.LogOutText, AppResources.LogOut, AppResources.Cancel))
			await _router.SignOutAsync();
	}
}
