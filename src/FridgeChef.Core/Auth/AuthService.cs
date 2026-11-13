using System.Text.Json;

namespace FridgeChef.Core.Auth;

// Where the session is kept between app launches (SecureStorage on the phone, a fake in tests).
public interface ISessionStore
{
    Task<string?> LoadAsync();
    Task SaveAsync(string value);
    Task ClearAsync();
}

// Holds the current session, keeps it fresh and persists it.
public sealed class AuthService(AuthClient client, ISessionStore store, Func<DateTimeOffset>? now = null)
{
    readonly Func<DateTimeOffset> _now = now ?? (() => DateTimeOffset.UtcNow);
    readonly object _gate = new();
    Session? _session;
    Task<Session?>? _refreshing;

    public AuthClient Client => client;
    public string? UserId => _session?.UserId;
    public bool IsSignedIn => _session is not null;

    // At app start: load the saved session (if any) and make sure it still works.
    public async Task<bool> RestoreAsync()
    {
        var json = await store.LoadAsync();
        try { _session = json is null ? null : JsonSerializer.Deserialize(json, AuthJson.Default.Session); }
        catch (JsonException) { _session = null; }
        return _session is not null && await GetAccessTokenAsync() is not null;
    }

    public async Task SetSessionAsync(Session s)
    {
        _session = s;
        await store.SaveAsync(JsonSerializer.Serialize(s, AuthJson.Default.Session));
    }

    // Returns a valid access token, refreshing it first when it is about to expire. Null = signed out.
    public async Task<string?> GetAccessTokenAsync()
    {
        var s = _session;
        if (s is null) return null;
        if (s.ExpiresAt - _now().ToUnixTimeSeconds() > 60) return s.AccessToken;

        Task<Session?> task;
        lock (_gate) task = _refreshing ??= RefreshAsync(s); // one refresh at a time: refresh tokens are single-use
        try { return (await task)?.AccessToken; }
        finally { lock (_gate) if (_refreshing == task) _refreshing = null; }
    }

    async Task<Session?> RefreshAsync(Session s)
    {
        try
        {
            var fresh = await client.RefreshAsync(s.RefreshToken);
            await SetSessionAsync(fresh);
            return fresh;
        }
        catch (AuthException e) when (e.Code == AuthException.Network)
        {
            return s; // offline: keep the session so saved content still works; retry next time
        }
        catch (AuthException)
        {
            await ClearAsync(); // the server rejected the session for real
            return null;
        }
    }

    public async Task<AccountInfo> GetAccountAsync() =>
        await client.GetUserAsync(await GetAccessTokenAsync() ?? throw new AuthException("not_signed_in"));

    // Change password (Figma 20): proves the current password by signing in with it, then sets the new one.
    public async Task ChangePasswordAsync(string currentPassword, string newPassword)
    {
        var account = await GetAccountAsync();
        if (!account.HasPassword) throw new AuthException("no_password");
        var session = await client.SignInAsync(account.Email, currentPassword); // invalid_credentials if wrong
        await SetSessionAsync(session);
        await client.UpdatePasswordAsync(session.AccessToken, newPassword);
    }

    // Log out: tell the server (best effort), then always forget the session on the phone.
    public async Task SignOutAsync()
    {
        if (_session is { } s)
            try { await client.SignOutAsync(s.AccessToken); } catch (AuthException) { }
        await ClearAsync();
    }

    async Task ClearAsync()
    {
        _session = null;
        await store.ClearAsync();
    }
}
