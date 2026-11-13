using FridgeChef.Core.ViewModels;

namespace FridgeChef.Views.Auth;

public partial class ProfileSetupPage : ContentPage
{
    public ProfileSetupPage(ProfileSetupViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        // The view model holds the photo as JPEG bytes (it has no MAUI types); show it here.
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.Avatar) && vm.Avatar is { } jpeg)
                AvatarImage.Source = ImageSource.FromStream(() => new MemoryStream(jpeg));
            else if (e.PropertyName == nameof(vm.ExistingAvatarUrl) && vm.Avatar is null && vm.ExistingAvatarUrl is { } url)
                AvatarImage.Source = url; // current photo when editing
        };
    }

    void OnBack(object? s, TappedEventArgs e) => _ = Navigation.PopAsync();
}
