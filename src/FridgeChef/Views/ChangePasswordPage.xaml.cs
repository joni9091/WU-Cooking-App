using FridgeChef.Core.ViewModels;

namespace FridgeChef.Views;

public partial class ChangePasswordPage : ContentPage
{
	public ChangePasswordPage(ChangePasswordViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}
