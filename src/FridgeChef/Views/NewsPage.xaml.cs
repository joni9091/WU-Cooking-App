using FridgeChef.Core.Recipes;

namespace FridgeChef.Views;

public partial class NewsPage : ContentPage
{
	readonly NewsViewModel _vm;

	public NewsPage(NewsViewModel vm)
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
