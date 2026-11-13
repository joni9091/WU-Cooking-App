using System.Net;
using FridgeChef.Core.Auth;
using FridgeChef.Core.ViewModels;

namespace FridgeChef.Tests;

sealed class MemoryStore : ISessionStore
{
    public string? Value;
    public Task<string?> LoadAsync() => Task.FromResult(Value);
    public Task SaveAsync(string v) { Value = v; return Task.CompletedTask; }
    public Task ClearAsync() { Value = null; return Task.CompletedTask; }
}

// Fake Supabase: answers every request with the given function, counts calls and remembers the paths.
sealed class FakeServer(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer) : HttpMessageHandler
{
    public int Calls;
    public readonly List<string> Paths = [];
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken c)
    {
        Interlocked.Increment(ref Calls);
        lock (Paths) Paths.Add(r.RequestUri!.PathAndQuery);
        return answer(r);
    }

    public static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
}

sealed class FakeNav : IAuthNavigator
{
    public int SignedIn;
    public string? LastPage;
    public Task SignedInAsync() { SignedIn++; return Task.CompletedTask; }
    public Task GoToAsync(string page) { LastPage = page; return Task.CompletedTask; }
}

sealed class FakeWeb(string? code) : IWebSignIn
{
    public Task<string?> GetCodeAsync(Uri url) => Task.FromResult(code);
}

sealed class FakePicker(byte[]? jpeg) : IPhotoPicker
{
    public Task<byte[]?> PickAvatarAsync() => Task.FromResult(jpeg);
}
