using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FridgeChef.Core.Auth;
using FridgeChef.Core.Recipes;
using FridgeChef.Core.Resources;

namespace FridgeChef.Core.ViewModels;

// Screens of the Account tab that the app opens; signing out / after deletion goes through the router.
public interface IAccountNavigator
{
    Task OpenAsync(string page);        // "edit-profile", "settings", "language", "change-password", "delete-account", "about"
    Task SignedOutAsync();              // clear the phone and show Log in (after Log out or Delete account)
}

// Figma 8. Account: photo, name, @handle, saved count (no daily-ideas counter: owner decision), rows.
public sealed partial class AccountViewModel(ProfileClient profiles, SavedRecipes saved, IAccountNavigator nav, IAppNavigator app) : ObservableObject
{
    [ObservableProperty] public partial string? DisplayName { get; set; }
    [ObservableProperty] public partial string? HandleText { get; set; }
    [ObservableProperty] public partial string? AvatarUrl { get; set; }
    [ObservableProperty] public partial string SavedCount { get; set; } = Format.Number(0);

    // Every time the tab opens (edit profile / saving may have changed things).
    public async Task AppearingAsync()
    {
        SavedCount = Format.Number(saved.Items.Count);
        try
        {
            if (await profiles.GetMineAsync() is { } p)
            {
                DisplayName = p.DisplayName;
                HandleText = "@" + p.Handle;
                AvatarUrl = p.AvatarUrl;
            }
            await saved.RefreshAsync();
            SavedCount = Format.Number(saved.Items.Count);
        }
        catch (AuthException) { } // offline: keep what is shown
    }

    [RelayCommand] Task Open(string page) => nav.OpenAsync(page);
    [RelayCommand] Task GoToSaved() => app.OpenTabAsync("saved");
}

// Figma 20. Change password: current, new, confirm.
public sealed partial class ChangePasswordViewModel(AuthService auth, IAppNavigator app, IToast toast) : ObservableObject
{
    [ObservableProperty] public partial string Current { get; set; } = "";
    [ObservableProperty] public partial string New { get; set; } = "";
    [ObservableProperty] public partial string Confirm { get; set; } = "";
    [ObservableProperty] public partial string? Error { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HidePassword))]
    public partial bool ShowPassword { get; set; }
    public bool HidePassword => !ShowPassword;

    [RelayCommand] void TogglePassword() => ShowPassword = !ShowPassword;

    [RelayCommand]
    async Task Update()
    {
        if (IsBusy) return;
        Error = null;
        if (string.IsNullOrEmpty(Current)) { Error = AppResources.ErrCurrentPassword; return; }
        if (!Validation.IsPassword(New)) { Error = AppResources.ErrPassword; return; }
        if (New != Confirm) { Error = AppResources.ErrPasswordMatch; return; }
        IsBusy = true;
        try
        {
            await auth.ChangePasswordAsync(Current, New);
            Current = New = Confirm = "";
            toast.Show(ToastKind.Success, AppResources.PasswordChanged);
            await app.BackAsync();
        }
        catch (AuthException e) when (e.Code == "invalid_credentials") { Error = AppResources.ErrCurrentPassword; }
        catch (AuthException e) when (e.Code == "same_password") { Error = AppResources.ErrSamePassword; }
        catch (AuthException e) { Error = AuthMessages.For(e); }
        finally { IsBusy = false; }
    }

    [RelayCommand] Task Back() => app.BackAsync();
}

// Figma 21. Delete account: type DELETE, then the delete-account function removes everything.
public sealed partial class DeleteAccountViewModel(Api api, IAccountNavigator nav, IAppNavigator app) : ObservableObject
{
    public const string ConfirmWord = "DELETE"; // same word in both languages, so it is never mistyped by translation

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    public partial string Confirm { get; set; } = "";
    [ObservableProperty] public partial string? Error { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    public bool CanDelete => Confirm.Trim() == ConfirmWord && !IsBusy;

    [RelayCommand]
    async Task Delete()
    {
        if (!CanDelete) return;
        IsBusy = true;
        Error = null;
        try
        {
            using var _ = await api.SendAsync(HttpMethod.Post, "functions/v1/delete-account");
            await nav.SignedOutAsync(); // the account is gone: forget it on the phone too
        }
        catch (AuthException e) { Error = AuthMessages.For(e); }
        finally { IsBusy = false; OnPropertyChanged(nameof(CanDelete)); }
    }

    [RelayCommand] Task Cancel() => app.BackAsync();
}
