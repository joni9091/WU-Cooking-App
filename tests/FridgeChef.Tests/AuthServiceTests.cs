using System.Net;
using FridgeChef.Core.Auth;

namespace FridgeChef.Tests;

public class AuthServiceTests
{
    static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    static HttpResponseMessage Json(HttpStatusCode code, string body) => FakeServer.Json(code, body);

    internal static string Token(string access, long expiresAt) =>
        $$$"""{"access_token":"{{{access}}}","refresh_token":"r2","expires_at":{{{expiresAt}}},"user":{"id":"u1"}}""";

    static AuthService Service(FakeServer server, MemoryStore store) =>
        new(new AuthClient(new HttpClient(server) { BaseAddress = new Uri("https://x.test/") }), store, () => Now);

    static async Task<AuthService> SignedInWithExpiredToken(FakeServer server, MemoryStore store)
    {
        var auth = Service(server, store);
        await auth.SetSessionAsync(new Session("old", "r1", Now.ToUnixTimeSeconds() - 10, "u1"));
        return auth;
    }

    [Fact]
    public async Task Fresh_token_is_used_without_calling_the_server()
    {
        var server = new FakeServer(_ => throw new InvalidOperationException());
        var auth = Service(server, new MemoryStore());
        await auth.SetSessionAsync(new Session("a1", "r1", Now.ToUnixTimeSeconds() + 3600, "u1"));
        Assert.Equal("a1", await auth.GetAccessTokenAsync());
        Assert.Equal(0, server.Calls);
    }

    [Fact]
    public async Task Concurrent_callers_share_one_refresh()
    {
        var gate = new TaskCompletionSource();
        var server = new FakeServer(async _ => { await gate.Task; return Json(HttpStatusCode.OK, Token("a2", Now.ToUnixTimeSeconds() + 3600)); });
        var store = new MemoryStore();
        var auth = await SignedInWithExpiredToken(server, store);

        var calls = Enumerable.Range(0, 5).Select(_ => auth.GetAccessTokenAsync()).ToArray();
        gate.SetResult();
        Assert.All(await Task.WhenAll(calls), t => Assert.Equal("a2", t));
        Assert.Equal(1, server.Calls);
        Assert.Contains("a2", store.Value); // new session saved for next launch
    }

    [Fact]
    public async Task No_internet_keeps_the_session()
    {
        var server = new FakeServer(_ => throw new HttpRequestException("offline"));
        var store = new MemoryStore();
        var auth = await SignedInWithExpiredToken(server, store);
        Assert.Equal("old", await auth.GetAccessTokenAsync());
        Assert.True(auth.IsSignedIn);
        Assert.NotNull(store.Value);
    }

    [Fact]
    public async Task Rejected_refresh_signs_out()
    {
        var server = new FakeServer(_ => Task.FromResult(Json(HttpStatusCode.BadRequest, """{"error_code":"refresh_token_not_found"}""")));
        var store = new MemoryStore();
        var auth = await SignedInWithExpiredToken(server, store);
        Assert.Null(await auth.GetAccessTokenAsync());
        Assert.False(auth.IsSignedIn);
        Assert.Null(store.Value);
    }

    [Fact]
    public async Task Restore_reads_the_saved_session()
    {
        var store = new MemoryStore();
        var first = Service(new FakeServer(_ => throw new InvalidOperationException()), store);
        await first.SetSessionAsync(new Session("a1", "r1", Now.ToUnixTimeSeconds() + 3600, "u1"));

        var relaunched = Service(new FakeServer(_ => throw new InvalidOperationException()), store);
        Assert.True(await relaunched.RestoreAsync());
        Assert.Equal("u1", relaunched.UserId);
    }

    [Fact]
    public async Task Sign_out_forgets_the_session_even_if_the_server_is_unreachable()
    {
        var store = new MemoryStore();
        var auth = Service(new FakeServer(_ => throw new HttpRequestException()), store);
        await auth.SetSessionAsync(new Session("a1", "r1", Now.ToUnixTimeSeconds() + 3600, "u1"));
        await auth.SignOutAsync();
        Assert.False(auth.IsSignedIn);
        Assert.Null(store.Value);
    }

    [Fact]
    public async Task Wrong_password_surfaces_the_server_code()
    {
        var server = new FakeServer(_ => Task.FromResult(Json(HttpStatusCode.BadRequest, """{"code":400,"error_code":"invalid_credentials","msg":"Invalid login credentials"}""")));
        var auth = Service(server, new MemoryStore());
        var e = await Assert.ThrowsAsync<AuthException>(() => auth.Client.SignInAsync("a@b.co", "12345678"));
        Assert.Equal("invalid_credentials", e.Code);
    }

    [Fact]
    public void Google_url_uses_pkce_and_the_app_callback()
    {
        var (url, verifier) = AuthClient.GoogleSignInUrl("https://x.test");
        Assert.Contains("provider=google", url.Query);
        Assert.Contains("code_challenge_method=s256", url.Query);
        Assert.Contains(Uri.EscapeDataString(AuthClient.RedirectUri), url.Query);
        Assert.InRange(verifier.Length, 43, 128); // PKCE spec length
    }
}
