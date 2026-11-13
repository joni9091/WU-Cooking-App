using FridgeChef.Core;
using FridgeChef.Core.Auth;
using FridgeChef.Core.ViewModels;
using FridgeChef.Services;
using FridgeChef.Views;
using FridgeChef.Views.Auth;
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

#if ANDROID
		// Figma inputs are plain grey boxes: remove Android's underline under every Entry.
		Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoUnderline", (h, _) =>
			h.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
#endif

		var s = builder.Services;
		// One HttpClient for all Supabase calls; the publishable key is sent with every request (RLS protects the data).
		s.AddSingleton(_ =>
		{
			var http = new HttpClient { BaseAddress = new Uri(SupabaseConfig.Url + "/"), Timeout = TimeSpan.FromSeconds(20) };
			http.DefaultRequestHeaders.Add("apikey", SupabaseConfig.PublishableKey);
			return http;
		});
		s.AddSingleton<AuthClient>();
		s.AddSingleton<ISessionStore, SecureSessionStore>();
		s.AddSingleton<AuthService>(sp => new AuthService(sp.GetRequiredService<AuthClient>(), sp.GetRequiredService<ISessionStore>()));
		s.AddSingleton(sp => new ProfileClient(sp.GetRequiredService<HttpClient>(), sp.GetRequiredService<AuthService>(), SupabaseConfig.Url));
		s.AddSingleton<IWebSignIn, WebSignIn>();
		s.AddSingleton<IPhotoPicker, PhotoPicker>();
		s.AddSingleton<Router>();
		s.AddSingleton<IAuthNavigator>(sp => sp.GetRequiredService<Router>());

		// Screens and their view models: new ones each time a screen opens.
		s.AddTransient<LoginViewModel>().AddTransient<SignUpViewModel>().AddTransient<ForgotPasswordViewModel>().AddTransient<ProfileSetupViewModel>();
		s.AddTransient<WelcomePage>().AddTransient<LanguagePage>().AddTransient<LoginPage>().AddTransient<SignUpPage>()
		 .AddTransient<ForgotPasswordPage>().AddTransient<ProfileSetupPage>().AddTransient<AccountPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
