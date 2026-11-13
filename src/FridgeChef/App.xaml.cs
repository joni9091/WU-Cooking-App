using FridgeChef.Services;

namespace FridgeChef;

public partial class App : Application
{
	readonly Router _router;

	public App(Router router)
	{
		InitializeComponent();
		UserAppTheme = ThemeService.IsDark ? AppTheme.Dark : AppTheme.Light; // Settings switch; light by default
		Language.ApplySaved(); // must run before any page is built
		_router = router;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Olive like the splash while the saved session is checked, then the router picks the first screen.
		var window = new Window(new ContentPage { BackgroundColor = Color.FromArgb("#8C6D00") });
		window.Created += async (_, _) => await _router.StartAsync();
		return window;
	}
}
