using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FridgeChef.Core.Auth;

public sealed record AccountInfo(string Email, string Provider)
{
    public bool HasPassword => Provider == "email"; // Google users have no password to change
}

// A logged-in session. ExpiresAt is Unix seconds (UTC).
public sealed record Session(string AccessToken, string RefreshToken, long ExpiresAt, string UserId);

// Error codes from Supabase Auth (error_code), plus "network" when the phone could not reach the server.
public sealed class AuthException(string code) : Exception(code)
{
    public const string Network = "network";
    public string Code => Message;
}

// Thin client for the Supabase Auth REST API (https://supabase.com/docs/reference/self-hosting-auth).
// Hand-written instead of a library: 8 small calls, and source-generated JSON keeps Release trimming/AOT safe.
public sealed class AuthClient(HttpClient http)
{
    public const string RedirectUri = "fridgechef://auth-callback";

    public Task SignUpAsync(string email, string password) =>
        SendAsync(HttpMethod.Post, "signup", new Creds { Email = email.Trim(), Password = password });

    public async Task<Session> SignInAsync(string email, string password) =>
        ToSession(await SendAsync(HttpMethod.Post, "token?grant_type=password", new Creds { Email = email.Trim(), Password = password }));

    public async Task<Session> RefreshAsync(string refreshToken) =>
        ToSession(await SendAsync(HttpMethod.Post, "token?grant_type=refresh_token", new Creds { RefreshToken = refreshToken }));

    // Emails a reset code (the "Reset password" email template shows {{ .Token }}).
    public Task SendResetCodeAsync(string email) =>
        SendAsync(HttpMethod.Post, "recover", new Creds { Email = email.Trim() });

    public async Task<Session> VerifyResetCodeAsync(string email, string code) =>
        ToSession(await SendAsync(HttpMethod.Post, "verify", new Creds { Email = email.Trim(), Token = code.Trim(), Type = "recovery" }));

    public Task UpdatePasswordAsync(string accessToken, string newPassword) =>
        SendAsync(HttpMethod.Put, "user", new Creds { Password = newPassword }, accessToken);

    public Task SignOutAsync(string accessToken) => SendAsync(HttpMethod.Post, "logout", null, accessToken);

    // Who is signed in: email and how they signed up ("email" or "google").
    public async Task<AccountInfo> GetUserAsync(string accessToken)
    {
        var user = Parse(await SendTextAsync(HttpMethod.Get, "user", null, accessToken), AuthJson.Default.UserInfo);
        return new AccountInfo(user?.Email ?? "", user?.AppMetadata?.Provider ?? "email");
    }

    // Google (PKCE): open this URL in the browser, then pass the returned ?code= to ExchangeCodeAsync.
    public static (Uri Url, string Verifier) GoogleSignInUrl(string supabaseUrl)
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier)));
        var url = $"{supabaseUrl}/auth/v1/authorize?provider=google&redirect_to={Uri.EscapeDataString(RedirectUri)}" +
                  $"&code_challenge={challenge}&code_challenge_method=s256";
        return (new Uri(url), verifier);
    }

    public async Task<Session> ExchangeCodeAsync(string code, string verifier) =>
        ToSession(await SendAsync(HttpMethod.Post, "token?grant_type=pkce", new Creds { AuthCode = code, CodeVerifier = verifier }));

    async Task<TokenResponse?> SendAsync(HttpMethod method, string path, Creds? body, string? accessToken = null)
    {
        var text = await SendTextAsync(method, path, body, accessToken);
        return text.Length > 0 && text.Contains("access_token") ? Parse(text, AuthJson.Default.TokenResponse) : null;
    }

    async Task<string> SendTextAsync(HttpMethod method, string path, Creds? body, string? accessToken)
    {
        using var req = new HttpRequestMessage(method, "auth/v1/" + path);
        if (body is not null) req.Content = JsonContent.Create(body, AuthJson.Default.Creds);
        if (accessToken is not null) req.Headers.Authorization = new("Bearer", accessToken);
        HttpResponseMessage res;
        try { res = await http.SendAsync(req); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { throw new AuthException(AuthException.Network); }
        using (res)
        {
            var text = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                var err = Parse(text, AuthJson.Default.ErrorResponse);
                throw new AuthException(err?.ErrorCode ?? err?.Error ?? $"http_{(int)res.StatusCode}");
            }
            return text;
        }
    }

    static T? Parse<T>(string text, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> info)
    {
        try { return JsonSerializer.Deserialize(text, info); } catch (JsonException) { return default; }
    }

    static Session ToSession(TokenResponse? t) =>
        t?.AccessToken is { } a && t.RefreshToken is { } r && t.User?.Id is { } id
            ? new Session(a, r, t.ExpiresAt, id)
            : throw new AuthException("bad_response");

    static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

sealed class Creds
{
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("password")] public string? Password { get; set; }
    [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
    [JsonPropertyName("token")] public string? Token { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("auth_code")] public string? AuthCode { get; set; }
    [JsonPropertyName("code_verifier")] public string? CodeVerifier { get; set; }
}

sealed class TokenResponse
{
    [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
    [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
    [JsonPropertyName("expires_at")] public long ExpiresAt { get; set; }
    [JsonPropertyName("user")] public UserInfo? User { get; set; }
}

sealed class UserInfo
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("app_metadata")] public AppMetadata? AppMetadata { get; set; }
}

sealed class AppMetadata { [JsonPropertyName("provider")] public string? Provider { get; set; } }

sealed class ErrorResponse
{
    [JsonPropertyName("error_code")] public string? ErrorCode { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
}

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Creds))]
[JsonSerializable(typeof(TokenResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(Session))]
[JsonSerializable(typeof(UserInfo))]
partial class AuthJson : JsonSerializerContext;
