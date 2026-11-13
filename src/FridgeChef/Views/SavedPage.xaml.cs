using FridgeChef.Core.ViewModels;

namespace FridgeChef.Views;

public partial class SavedPage : ContentPage
{
	readonly SavedViewModel _vm;

	public SavedPage(SavedViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	// Every time the tab opens: phone copy first (instant, offline), then the server.
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.AppearingAsync();
	}
}
