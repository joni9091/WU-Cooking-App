namespace FridgeChef;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
	}

#if ANDROID
	// Restyle the tab bar once the screen has settled (the bar is created after the first navigation starts).
	protected override void OnNavigated(ShellNavigatedEventArgs args)
	{
		base.OnNavigated(args);
		Dispatcher.Dispatch(() => (CurrentItem?.Handler as FloatingTabBarHandler)?.ApplyFigmaStyle());
	}
#endif
}
