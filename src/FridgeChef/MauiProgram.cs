using Microsoft.Extensions.Logging;

namespace FridgeChef;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				// Second name = the alias used in Theme.xaml.
				fonts.AddFont("InstrumentSerif-Regular.ttf", "InstrumentSerif");
				fonts.AddFont("InstrumentSerif-Italic.ttf", "InstrumentSerifItalic");
				fonts.AddFont("KantumruyPro-Regular.ttf", "KantumruyPro");
				fonts.AddFont("KantumruyPro-SemiBold.ttf", "KantumruyProSemiBold");
				fonts.AddFont("Inter-Regular.ttf", "Inter");
				fonts.AddFont("Inter-SemiBold.ttf", "InterSemiBold");
			})
			.ConfigureMauiHandlers(handlers =>
			{
#if ANDROID
				handlers.AddHandler<ShellItem, FloatingTabBarHandler>(); // Figma's floating tab bar
#endif
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
