using FridgeChef.Core.ViewModels;

namespace FridgeChef.Views;

public partial class FridgePage : ContentPage
{
	readonly FridgeViewModel _vm;

	public FridgePage(FridgeViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.AppearingAsync(); // brings back last time's fridge and cuisines
	}
}
