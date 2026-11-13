using FridgeChef.Core.Auth;
using FridgeChef.Core.Recipes;
using FridgeChef.Core.ViewModels;
using FridgeChef.Views.Auth;

namespace FridgeChef.Services;

// Decides which screen the app shows. The single gate, used at launch and after every sign-in:
//   no session -> Welcome (first time) or Log in;  session but no @handle -> Profile setup;  otherwise -> tabs.
public sealed class Router(IServiceProvider services, AuthService auth, ProfileClient profiles, LocalCache cache, SavedRecipes saved) : IAuthNavigator
{
    const string WelcomeSeen = "welcome_seen";
    static string ProfileDone(string userId) => "profile_done_" + userId; // lets the app open offline

    static Window Window => Application.Current!.Windows[0];
    NavigationPage? _authFlow;

    T Get<T>() where T : notnull => services.GetRequiredService<T>();

    public async Task StartAsync()
    {
        if (await auth.RestoreAsync()) await SignedInAsync();
        else ShowSignedOut();
    }

    public async Task SignedInAsync()
    {
        bool complete;
        try { complete = (await profiles.GetMineAsync())?.IsComplete == true; }
        catch (AuthException) when (auth.IsSignedIn && auth.UserId is { } id)
        {
            complete = Preferences.Get(ProfileDone(id), false); // offline: trust what we knew last time
        }
        catch (AuthException) { ShowSignedOut(); return; } // session rejected

        if (!complete) { Show(new NavigationPage(Get<ProfileSetupPage>())); return; }
        Preferences.Set(ProfileDone(auth.UserId!), true);
        _authFlow = null;
        Show(new AppShell());
    }

    public async Task SignOutAsync()
    {
        await auth.SignOutAsync();
        saved.Clear();
        await cache.ClearAsync(); // the next person on this phone must not see this user's saved recipes
        ShowSignedOut();
    }

    // After a language change: rebuild whatever is showing so every text is re-read.
    public void Rebuild()
    {
        if (Window.Page is AppShell) Show(new AppShell());
        else if (_authFlow is not null) ShowSignedOut();
        else Show(new NavigationPage(Get<ProfileSetupPage>()));
    }

    public Task GoToAsync(string page)
    {
        var nav = _authFlow!;
        switch (page)
        {
            case "back": return nav.PopAsync();
            case "signup": return nav.PushAsync(Get<SignUpPage>());
            case "forgot": return nav.PushAsync(Get<ForgotPasswordPage>());
            case "language": return nav.PushAsync(Get<LanguagePage>());
            case "started": // Welcome finished: from now on the app opens on Log in
                Preferences.Set(WelcomeSeen, true);
                ShowSignedOut();
                return Task.CompletedTask;
            case "login": // back to Log in if it is already open underneath, otherwise open it
                return nav.Navigation.NavigationStack.Any(p => p is LoginPage) ? nav.PopToRootAsync() : nav.PushAsync(Get<LoginPage>());
            default: throw new ArgumentException(page);
        }
    }

    void ShowSignedOut()
    {
        Page first = Preferences.Get(WelcomeSeen, false) ? Get<LoginPage>() : Get<WelcomePage>();
        _authFlow = new NavigationPage(first);
        Show(_authFlow);
    }

    static void Show(Page page) => MainThread.BeginInvokeOnMainThread(() => Window.Page = page);
}
