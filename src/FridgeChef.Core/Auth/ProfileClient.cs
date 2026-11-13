using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FridgeChef.Core.Auth;

public sealed record Profile(string? Handle, string? DisplayName, string? AvatarUrl)
{
    // Profile setup is finished once a handle is saved (the signup trigger creates the row empty).
    public bool IsComplete => !string.IsNullOrEmpty(Handle);
}

// The signed-in user's own profile row and avatar (Row Level Security limits both to this user).
public sealed class ProfileClient(HttpClient http, AuthService auth, string supabaseUrl)
{
    public const string HandleTaken = "handle_taken";

    public async Task<Profile?> GetMineAsync()
    {
        using var res = await SendAsync(HttpMethod.Get, $"rest/v1/profiles?select=handle,display_name,avatar_url&id=eq.{auth.UserId}");
        var rows = await res.Content.ReadFromJsonAsync(ProfileJson.Default.ProfileRowArray);
        return rows is [var r, ..] ? new Profile(r.Handle, r.DisplayName, r.AvatarUrl) : null;
    }

    public async Task<bool> IsHandleAvailableAsync(string handle)
    {
        using var res = await SendAsync(HttpMethod.Post, "rest/v1/rpc/is_handle_available",
            JsonContent.Create(new HandleArg { H = Validation.NormalizeHandle(handle) }, ProfileJson.Default.HandleArg));
        return await res.Content.ReadFromJsonAsync(ProfileJson.Default.Boolean);
    }

    // Throws AuthException(HandleTaken) when someone else got the handle first.
    public async Task SaveAsync(string handle, string displayName, string? avatarUrl)
    {
        var row = new ProfileRow { Handle = Validation.NormalizeHandle(handle), DisplayName = displayName.Trim(), AvatarUrl = avatarUrl };
        using var res = await SendAsync(HttpMethod.Patch, $"rest/v1/profiles?id=eq.{auth.UserId}",
            JsonContent.Create(row, ProfileJson.Default.ProfileRow), allowConflict: true);
        if (res.StatusCode == HttpStatusCode.Conflict) throw new AuthException(HandleTaken); // unique index on handle
    }

    // Uploads a ready-made JPEG (already 300 px, < 200 KB) to avatars/<user id>/avatar.jpg and returns its public URL.
    public async Task<string> UploadAvatarAsync(byte[] jpeg)
    {
        var path = $"{auth.UserId}/avatar.jpg";
        var content = new ByteArrayContent(jpeg);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        using var res = await SendAsync(HttpMethod.Post, $"storage/v1/object/avatars/{path}", content, upsert: true);
        // ?v= makes phones reload the picture after it changes (same file name).
        return $"{supabaseUrl}/storage/v1/object/public/avatars/{path}?v={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    }

    async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? body = null, bool upsert = false, bool allowConflict = false)
    {
        var token = await auth.GetAccessTokenAsync() ?? throw new AuthException("not_signed_in");
        var req = new HttpRequestMessage(method, path) { Content = body };
        req.Headers.Authorization = new("Bearer", token);
        if (upsert) req.Headers.Add("x-upsert", "true");
        if (method == HttpMethod.Patch) req.Headers.Add("Prefer", "return=minimal");
        HttpResponseMessage res;
        try { res = await http.SendAsync(req); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { throw new AuthException(AuthException.Network); }
        if (res.IsSuccessStatusCode || (allowConflict && res.StatusCode == HttpStatusCode.Conflict)) return res;
        res.Dispose();
        throw new AuthException($"http_{(int)res.StatusCode}");
    }
}

sealed class ProfileRow
{
    [JsonPropertyName("handle")] public string? Handle { get; set; }
    [JsonPropertyName("display_name")] public string? DisplayName { get; set; }
    [JsonPropertyName("avatar_url")] public string? AvatarUrl { get; set; }
}

sealed class HandleArg { [JsonPropertyName("h")] public string? H { get; set; } }

[JsonSerializable(typeof(ProfileRow))]
[JsonSerializable(typeof(ProfileRow[]))]
[JsonSerializable(typeof(HandleArg))]
[JsonSerializable(typeof(bool))]
partial class ProfileJson : JsonSerializerContext;
