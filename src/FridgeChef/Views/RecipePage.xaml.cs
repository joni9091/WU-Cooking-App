using FridgeChef.Core.ViewModels;

namespace FridgeChef.Views;

public partial class RecipePage : ContentPage
{
	readonly RecipesViewModel _vm;

	public RecipePage(RecipesViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.AppearingAsync();
	}
}
