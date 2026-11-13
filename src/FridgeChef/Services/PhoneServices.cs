using FridgeChef.Core.Auth;
using FridgeChef.Core.Resources;
using FridgeChef.Core.ViewModels;

namespace FridgeChef.Services;

// The login session lives in SecureStorage (encrypted by Android Keystore), never in plain preferences.
public sealed class SecureSessionStore : ISessionStore
{
    const string Key = "session";
    public Task<string?> LoadAsync() => SecureStorage.Default.GetAsync(Key);
    public Task SaveAsync(string value) => SecureStorage.Default.SetAsync(Key, value);
    public Task ClearAsync() { SecureStorage.Default.Remove(Key); return Task.CompletedTask; }
}

// "Continue with Google": opens Google in a browser tab and waits for fridgechef://auth-callback.
public sealed class WebSignIn : IWebSignIn
{
    public async Task<string?> GetCodeAsync(Uri url)
    {
        try
        {
            var result = await WebAuthenticator.Default.AuthenticateAsync(new WebAuthenticatorOptions
            {
                Url = url,
                CallbackUrl = new Uri(AuthClient.RedirectUri),
            });
            if (result.Properties.TryGetValue("code", out var code)) return code;
            throw new AuthException(result.Properties.TryGetValue("error_code", out var e) ? e : "oauth_failed");
        }
        catch (TaskCanceledException) { return null; } // user closed the browser
    }
}

// Profile photo: gallery or camera, then a square 300 px JPEG under 200 KB (made by AvatarImage on Android).
public sealed class PhotoPicker : IPhotoPicker
{
    public async Task<byte[]?> PickAvatarAsync()
    {
        var page = Application.Current!.Windows[0].Page!;
        var choice = await page.DisplayActionSheetAsync(null, AppResources.Cancel, null, AppResources.PhotoFromGallery, AppResources.PhotoFromCamera);
        FileResult? file = choice == AppResources.PhotoFromGallery ? (await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { SelectionLimit = 1 }))?.FirstOrDefault()
                         : choice == AppResources.PhotoFromCamera && MediaPicker.Default.IsCaptureSupported ? await MediaPicker.Default.CapturePhotoAsync()
                         : null;
        if (file is null) return null;
        await using var stream = await file.OpenReadAsync();
        var copy = new MemoryStream();
        await stream.CopyToAsync(copy);
        return await Task.Run(() => AvatarImage.ToSquareJpeg(copy.ToArray()));
    }
}
