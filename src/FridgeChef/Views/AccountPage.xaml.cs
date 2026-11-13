using FridgeChef.Core.ViewModels;
using FridgeChef.Services;

namespace FridgeChef.Views;

public partial class AccountPage : ContentPage
{
	readonly AccountViewModel _vm;

	public AccountPage(AccountViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		LanguageRow.Value = Language.Current == "km" ? "ខ្មែរ" : "English";
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.AppearingAsync(); // refresh after Edit profile or saving recipes
	}
}
