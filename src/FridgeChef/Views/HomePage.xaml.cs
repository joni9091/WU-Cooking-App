using FridgeChef.Core.ViewModels;

namespace FridgeChef.Views;

public partial class HomePage : ContentPage
{
	readonly HomeViewModel _vm;

	public HomePage(HomeViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.AppearingAsync(); // loads once; pull down to refresh
	}
}
