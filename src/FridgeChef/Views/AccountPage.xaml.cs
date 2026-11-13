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

	async void OnLogOut(object? sender, EventArgs e)
	{
		if (await DisplayAlertAsync(AppResources.LogOutTitle, AppResources.LogOutText, AppResources.LogOut, AppResources.Cancel))
			await _router.SignOutAsync();
	}
}
