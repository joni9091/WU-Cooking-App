using FridgeChef.Services;

namespace FridgeChef;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		// shortcut: Figma only has a light design, so the app stays light even in phone dark mode; remove when dark mode is designed (Phase 7).
		UserAppTheme = AppTheme.Light;
		Language.ApplySaved(); // must run before any page is built
	}

	protected override Window CreateWindow(IActivationState? activationState) => new(new AppShell());
}
