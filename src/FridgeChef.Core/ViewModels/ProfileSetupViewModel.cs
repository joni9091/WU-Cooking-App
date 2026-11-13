using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FridgeChef.Core.Auth;
using FridgeChef.Core.Resources;

namespace FridgeChef.Core.ViewModels;

public interface IPhotoPicker
{
    // Lets the user pick or take a photo; returns a square 300 px JPEG under 200 KB, or null if cancelled.
    Task<byte[]?> PickAvatarAsync();
}

// Step 2 of sign-up: @handle, account name and optional photo. Required once per account.
public sealed partial class ProfileSetupViewModel(ProfileClient profiles, IAuthNavigator nav, IPhotoPicker picker) : ObservableObject
{
    public TimeSpan CheckDelay { get; init; } = TimeSpan.FromMilliseconds(400); // wait for the user to stop typing
    CancellationTokenSource? _check;

    [ObservableProperty] public partial string Handle { get; set; } = "";
    [ObservableProperty] public partial string DisplayName { get; set; } = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PhotoLabel))]
    public partial byte[]? Avatar { get; set; }

    public string PhotoLabel => Avatar is null ? AppResources.AddPhoto : AppResources.ChangePhoto;
    [ObservableProperty] public partial string? HandleStatus { get; set; }
    [ObservableProperty] public partial bool HandleIsBad { get; set; } // shows the handle message in red
    [ObservableProperty] public partial string? Error { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }

    // Live "Available / taken" check while typing.
    partial void OnHandleChanged(string value)
    {
        // Only allowed characters can be typed: lower-case a-z, 0-9, dot, underscore (dashes, spaces, @ are dropped).
        var clean = new string(value.ToLowerInvariant().Where(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '_').ToArray());
        if (clean != value) { Handle = clean; return; } // this re-runs with the cleaned text
        _check?.Cancel();
        HandleStatus = null;
        HandleIsBad = false;
        if (!Validation.IsHandle(value)) { if (value.Length > 0) { HandleStatus = AppResources.ErrHandle; HandleIsBad = true; } return; }
        _check = new CancellationTokenSource();
        _ = CheckAsync(value, _check.Token);
    }

    async Task CheckAsync(string handle, CancellationToken ct)
    {
        try
        {
            HandleStatus = AppResources.HandleChecking;
            await Task.Delay(CheckDelay, ct);
            var free = await profiles.IsHandleAvailableAsync(handle);
            if (!ct.IsCancellationRequested) { HandleStatus = free ? AppResources.HandleFree : AppResources.HandleTaken; HandleIsBad = !free; }
        }
        catch (OperationCanceledException) { }
        catch (AuthException) { if (!ct.IsCancellationRequested) HandleStatus = null; } // offline: Finish will tell
    }

    [RelayCommand]
    async Task PickPhoto()
    {
        Error = null;
        try { if (await picker.PickAvatarAsync() is { } jpeg) Avatar = jpeg; }
        catch (Exception) { Error = AppResources.ErrPhoto; } // unreadable file, camera refused, ...
    }

    [RelayCommand]
    async Task Finish()
    {
        if (IsBusy) return;
        Error = null;
        if (!Validation.IsHandle(Handle)) { HandleStatus = AppResources.ErrHandle; HandleIsBad = true; return; } // shown under the handle box
        if (!Validation.IsDisplayName(DisplayName)) { Error = AppResources.ErrName; return; }
        IsBusy = true;
        try
        {
            var url = Avatar is { } jpeg ? await profiles.UploadAvatarAsync(jpeg) : null;
            await profiles.SaveAsync(Handle, DisplayName, url);
            await nav.SignedInAsync();
        }
        catch (AuthException e) when (e.Code == ProfileClient.HandleTaken) { HandleStatus = AppResources.HandleTaken; HandleIsBad = true; }
        catch (AuthException e) { Error = AuthMessages.For(e); }
        finally { IsBusy = false; }
    }
}
