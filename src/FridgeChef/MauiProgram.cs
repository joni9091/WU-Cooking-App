using FridgeChef.Core;
using FridgeChef.Core.Auth;
using FridgeChef.Core.Recipes;
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
				fonts.AddFont("ClashGrotesk-Regular.ttf", "ClashGrotesk");
				fonts.AddFont("ClashGrotesk-Medium.ttf", "ClashGroteskMedium");
				fonts.AddFont("ClashGrotesk-Semibold.ttf", "ClashGroteskSemibold");
				fonts.AddFont("KantumruyPro-Regular.ttf", "KantumruyPro");
				fonts.AddFont("KantumruyPro-SemiBold.ttf", "KantumruyProSemiBold");
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
			var http = new HttpClient { BaseAddress = new Uri(SupabaseConfig.Url + "/"), Timeout = TimeSpan.FromSeconds(60) }; // recipe ideas can take up to 2 x 15 s on the server
			http.DefaultRequestHeaders.Add("apikey", SupabaseConfig.PublishableKey);
			return http;
		});
		s.AddSingleton<AuthClient>();
		s.AddSingleton<ISessionStore, SecureSessionStore>();
		s.AddSingleton<AuthService>(sp => new AuthService(sp.GetRequiredService<AuthClient>(), sp.GetRequiredService<ISessionStore>()));
		s.AddSingleton<Api>();
		s.AddSingleton(sp => new ProfileClient(sp.GetRequiredService<Api>(), SupabaseConfig.Url));
		s.AddSingleton<IWebSignIn, WebSignIn>();
		s.AddSingleton<IPhotoPicker, PhotoPicker>();
		s.AddSingleton(_ => new LocalCache(Path.Combine(FileSystem.AppDataDirectory, "fridgechef.db3")));
		s.AddSingleton<RecipeClient>();
		s.AddSingleton<SavedRecipes>();
		s.AddSingleton<SuggestClient>();
		s.AddSingleton<AppNavigator>();
		s.AddSingleton<IAppNavigator>(sp => sp.GetRequiredService<AppNavigator>());
		s.AddSingleton<INewsNavigator>(sp => sp.GetRequiredService<AppNavigator>());
		s.AddSingleton<NewsClient>();
		s.AddSingleton<IToast, ToastService>();
		s.AddSingleton<Router>();
		s.AddSingleton<IAuthNavigator>(sp => sp.GetRequiredService<Router>());
		s.AddSingleton<IAccountNavigator>(sp => sp.GetRequiredService<Router>());

		// Screens and their view models: new ones each time a screen opens.
		s.AddTransient<LoginViewModel>().AddTransient<SignUpViewModel>().AddTransient<ForgotPasswordViewModel>().AddTransient<ProfileSetupViewModel>();
		s.AddTransient<WelcomePage>().AddTransient<LanguagePage>().AddTransient<LoginPage>().AddTransient<SignUpPage>()
		 .AddTransient<ForgotPasswordPage>().AddTransient<ProfileSetupPage>().AddTransient<AccountPage>();
		s.AddTransient<HomeViewModel>().AddTransient<RecipesViewModel>().AddTransient<RecipeDetailViewModel>().AddTransient<SavedViewModel>();
		s.AddTransient<HomePage>().AddTransient<RecipePage>().AddTransient<RecipeDetailPage>().AddTransient<SavedPage>();
		s.AddTransient<FridgeViewModel>().AddTransient<SuggestionsViewModel>().AddTransient<SuggestionDetailViewModel>();
		s.AddTransient<FridgePage>().AddTransient<SuggestionsPage>().AddTransient<SuggestionDetailPage>();
		// Routes: registered once per app run (AppShell is rebuilt often).
		Routing.RegisterRoute(AppNavigator.DetailRoute, typeof(RecipeDetailPage));
		Routing.RegisterRoute(AppNavigator.FridgeRoute, typeof(FridgePage));
		Routing.RegisterRoute(AppNavigator.SuggestionsRoute, typeof(SuggestionsPage));
		Routing.RegisterRoute(AppNavigator.SuggestionRoute, typeof(SuggestionDetailPage));
		s.AddTransient<NewsViewModel>().AddTransient<NewsArticleViewModel>().AddTransient<NewsPage>().AddTransient<NewsArticlePage>();
		Routing.RegisterRoute(AppNavigator.ArticleRoute, typeof(NewsArticlePage));
		s.AddTransient<AccountViewModel>().AddTransient<ChangePasswordViewModel>().AddTransient<DeleteAccountViewModel>();
		s.AddTransient<SettingsPage>().AddTransient<ChangePasswordPage>().AddTransient<DeleteAccountPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
