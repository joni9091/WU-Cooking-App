using System.Net;

namespace FridgeChef.Core.Auth;

// Sends a request to Supabase (database, storage or functions) as the signed-in user.
// Failures become AuthException: "network" when offline, "http_<code>" otherwise.
// A request cancelled by the user throws OperationCanceledException (no error message to show).
public sealed class Api(HttpClient http, AuthService auth)
{
    public AuthService Auth => auth;

    public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? body = null,
        Action<HttpRequestMessage>? configure = null, params HttpStatusCode[] alsoOk) =>
        SendAsync(method, path, body, configure, CancellationToken.None, alsoOk);

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? body,
        Action<HttpRequestMessage>? configure, CancellationToken cancel, params HttpStatusCode[] alsoOk)
    {
        var token = await auth.GetAccessTokenAsync() ?? throw new AuthException("not_signed_in");
        using var req = new HttpRequestMessage(method, path) { Content = body };
        req.Headers.Authorization = new("Bearer", token);
        configure?.Invoke(req);
        HttpResponseMessage res;
        try { res = await http.SendAsync(req, cancel); }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested) { throw; } // user pressed Cancel
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { throw new AuthException(AuthException.Network); }
        if (res.IsSuccessStatusCode || alsoOk.Contains(res.StatusCode)) return res;
        res.Dispose();
        throw new AuthException($"http_{(int)res.StatusCode}");
    }
}
