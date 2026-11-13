using FridgeChef.Core.ViewModels;

namespace FridgeChef.Views.Auth;

public partial class SignUpPage : ContentPage
{
    public SignUpPage(SignUpViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
