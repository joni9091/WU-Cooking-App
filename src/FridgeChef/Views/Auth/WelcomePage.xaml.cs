using FridgeChef.Core.Resources;
using FridgeChef.Services;

namespace FridgeChef.Views.Auth;

public sealed record WelcomeSlide(string Image, string Title, string Text);

public partial class WelcomePage : ContentPage
{
    readonly Router _router;

    public WelcomePage(Router router)
    {
        InitializeComponent();
        _router = router;
        // Slide 1 is the Figma design; slides 2-4 were written to match it (owner request).
        Slides.ItemsSource = new WelcomeSlide[]
        {
            new("welcome_1.jpg", AppResources.Welcome1Title, AppResources.Welcome1Text),
            new("welcome_2.jpg", AppResources.Welcome2Title, AppResources.Welcome2Text),
            new("welcome_3.jpg", AppResources.Welcome3Title, AppResources.Welcome3Text),
            new("welcome_4.jpg", AppResources.Welcome4Title, AppResources.Welcome4Text),
        };
    }

    void OnGetStarted(object? sender, EventArgs e) => _ = _router.GoToAsync("started");

    void OnLanguageTapped(object? sender, TappedEventArgs e) => _ = _router.GoToAsync("language");
}
