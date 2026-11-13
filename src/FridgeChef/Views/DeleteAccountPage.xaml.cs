using FridgeChef.Core.ViewModels;

namespace FridgeChef.Views;

public partial class DeleteAccountPage : ContentPage
{
	public DeleteAccountPage(DeleteAccountViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}
