using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FridgeChef.Core.Auth;
using FridgeChef.Core.Resources;

namespace FridgeChef.Core.ViewModels;

// Things only the phone can do; the app provides them, tests provide fakes.
public interface IAuthNavigator
{
    Task SignedInAsync();          // decide where to go now: Profile setup or the tabs
    Task GoToAsync(string page);   // "login", "signup", "forgot", "back"
}

public interface IWebSignIn
{
    // Opens the Google page and returns the ?code= from the callback, or null if the user closed it.
    Task<string?> GetCodeAsync(Uri url);
}

// Friendly, translated text for each error. Never reveals whether an email has an account.
public static class AuthMessages
{
    public static string For(AuthException e) => e.Code switch
    {
        "invalid_credentials" => AppResources.ErrLogin,
        "email_not_confirmed" => AppResources.ErrNotConfirmed,
        AuthException.Network => AppResources.ErrNoInternet,
        "over_email_send_rate_limit" or "over_request_rate_limit" or "http_429" => AppResources.ErrTooMany,
        "otp_expired" or "otp_disabled" => AppResources.ErrCodeWrong,
        "weak_password" => AppResources.ErrPassword,
        "email_address_not_authorized" => AppResources.ErrEmailNotAllowed,
        ProfileClient.HandleTaken => AppResources.HandleTaken,
        _ => AppResources.ErrGeneric,
    };
}

public abstract partial class AuthViewModel(AuthService auth, IAuthNavigator nav, IWebSignIn web) : ObservableObject
{
    protected AuthService Auth => auth;
    protected IAuthNavigator Nav => nav;

    [ObservableProperty] public partial string? Error { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HidePassword), nameof(EyeIcon), nameof(ShowPasswordText))]
    public partial bool ShowPassword { get; set; }

    public bool HidePassword => !ShowPassword;
    public string EyeIcon => ShowPassword ? "ic_eye.png" : "ic_eye_off.png"; // Figma: crossed eye while hidden
    public string ShowPasswordText => ShowPassword ? AppResources.HidePassword : AppResources.ShowPassword;

    // Runs a server call: shows the busy state, turns failures into a friendly message.
    protected async Task RunAsync(Func<Task> work)
    {
        if (IsBusy) return;
        Error = null;
        IsBusy = true;
        try { await work(); }
        catch (AuthException e) { Error = AuthMessages.For(e); }
        finally { IsBusy = false; }
    }

    [RelayCommand] void TogglePassword() => ShowPassword = !ShowPassword;

    [RelayCommand] Task Go(string page) => nav.GoToAsync(page);

    [RelayCommand]
    Task Google() => RunAsync(async () =>
    {
        var (url, verifier) = AuthClient.GoogleSignInUrl(SupabaseConfig.Url);
        if (await web.GetCodeAsync(url) is not { } code) return; // closed by the user: nothing to report
        await auth.SetSessionAsync(await auth.Client.ExchangeCodeAsync(code, verifier));
        await nav.SignedInAsync();
    });
}

public sealed partial class LoginViewModel(AuthService auth, IAuthNavigator nav, IWebSignIn web) : AuthViewModel(auth, nav, web)
{
    [ObservableProperty] public partial string Email { get; set; } = "";
    [ObservableProperty] public partial string Password { get; set; } = "";

    [RelayCommand]
    Task SignIn() => RunAsync(async () =>
    {
        if (!Validation.IsEmail(Email)) { Error = AppResources.ErrEmail; return; }
        if (string.IsNullOrEmpty(Password)) { Error = AppResources.ErrPassword; return; }
        await Auth.SetSessionAsync(await Auth.Client.SignInAsync(Email, Password));
        Password = "";
        await Nav.SignedInAsync();
    });
}

public sealed partial class SignUpViewModel(AuthService auth, IAuthNavigator nav, IWebSignIn web) : AuthViewModel(auth, nav, web)
{
    [ObservableProperty] public partial string Email { get; set; } = "";
    [ObservableProperty] public partial string Password { get; set; } = "";
    [ObservableProperty] public partial string Confirm { get; set; } = "";
    [ObservableProperty] public partial string? Info { get; set; }

    [RelayCommand]
    Task SignUp() => RunAsync(async () =>
    {
        Info = null;
        if (!Validation.IsEmail(Email)) { Error = AppResources.ErrEmail; return; }
        if (!Validation.IsPassword(Password)) { Error = AppResources.ErrPassword; return; }
        if (Password != Confirm) { Error = AppResources.ErrPasswordMatch; return; }
        await Auth.Client.SignUpAsync(Email, Password);
        Password = Confirm = "";
        Info = AppResources.SignUpDone; // same message whether or not the email was already used
    });
}

public sealed partial class ForgotPasswordViewModel(AuthService auth, IAuthNavigator nav, IWebSignIn web) : AuthViewModel(auth, nav, web)
{
    [ObservableProperty] public partial string Email { get; set; } = "";
    [ObservableProperty] public partial string Code { get; set; } = "";
    [ObservableProperty] public partial string NewPassword { get; set; } = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AskEmail))]
    public partial bool CodeSent { get; set; }

    public bool AskEmail => !CodeSent; // step 1 (email) is shown until the code is sent

    [RelayCommand]
    Task SendCode() => RunAsync(async () =>
    {
        if (!Validation.IsEmail(Email)) { Error = AppResources.ErrEmail; return; }
        await Auth.Client.SendResetCodeAsync(Email);
        CodeSent = true; // shown even if no account uses this email
    });

    [RelayCommand]
    Task SavePassword() => RunAsync(async () =>
    {
        if (Code.Trim().Length < 6) { Error = AppResources.ErrCode; return; }
        if (!Validation.IsPassword(NewPassword)) { Error = AppResources.ErrPassword; return; }
        var session = await Auth.Client.VerifyResetCodeAsync(Email, Code);
        await Auth.SetSessionAsync(session);
        await Auth.Client.UpdatePasswordAsync(session.AccessToken, NewPassword);
        NewPassword = "";
        await Nav.SignedInAsync();
    });
}
