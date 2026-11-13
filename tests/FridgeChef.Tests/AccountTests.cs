using System.Net;
using FridgeChef.Core.Auth;
using FridgeChef.Core.Resources;
using FridgeChef.Core.ViewModels;

namespace FridgeChef.Tests;

sealed class FakeAccountNav : IAccountNavigator
{
    public int SignedOut;
    public Task OpenAsync(string page) => Task.CompletedTask;
    public Task SignedOutAsync() { SignedOut++; return Task.CompletedTask; }
}

[Collection("Language")]
public class AccountTests
{
    static readonly long Later = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();

    static async Task<(Api api, FakeServer server)> SignedIn(Func<HttpRequestMessage, HttpResponseMessage> answer)
    {
        var server = new FakeServer(r => Task.FromResult(answer(r)));
        var http = new HttpClient(server) { BaseAddress = new Uri("https://x.test/") };
        var auth = new AuthService(new AuthClient(http), new MemoryStore());
        await auth.SetSessionAsync(new Session("a", "r", Later, "u1"));
        return (new Api(http, auth), server);
    }

    const string EmailUser = """{"id":"u1","email":"a@b.co","app_metadata":{"provider":"email"}}""";

    [Fact]
    public async Task Wrong_current_password_is_explained()
    {
        var (api, _) = await SignedIn(r => r.RequestUri!.AbsolutePath.EndsWith("/user") && r.Method == HttpMethod.Get
            ? FakeServer.Json(HttpStatusCode.OK, EmailUser)
            : FakeServer.Json(HttpStatusCode.BadRequest, """{"error_code":"invalid_credentials"}"""));
        var vm = new ChangePasswordViewModel(api.Auth, new FakeAppNav(), new FakeToast()) { Current = "wrongpass", New = "12345678", Confirm = "12345678" };
        await vm.UpdateCommand.ExecuteAsync(null);
        Assert.Equal(AppResources.ErrCurrentPassword, vm.Error);
    }

    [Fact]
    public async Task Google_accounts_have_no_password_to_change()
    {
        var (api, _) = await SignedIn(_ => FakeServer.Json(HttpStatusCode.OK, """{"id":"u1","email":"a@b.co","app_metadata":{"provider":"google"}}"""));
        Assert.False((await api.Auth.GetAccountAsync()).HasPassword);
        var e = await Assert.ThrowsAsync<AuthException>(() => api.Auth.ChangePasswordAsync("x", "12345678"));
        Assert.Equal("no_password", e.Code);
    }

    [Fact]
    public async Task New_passwords_must_match()
    {
        var (api, server) = await SignedIn(_ => throw new InvalidOperationException());
        var vm = new ChangePasswordViewModel(api.Auth, new FakeAppNav(), new FakeToast()) { Current = "old-pass1", New = "12345678", Confirm = "1234567x" };
        await vm.UpdateCommand.ExecuteAsync(null);
        Assert.Equal(AppResources.ErrPasswordMatch, vm.Error);
        Assert.Equal(0, server.Calls);
    }

    [Fact]
    public async Task Delete_needs_the_exact_word_then_clears_the_phone()
    {
        var (api, server) = await SignedIn(_ => FakeServer.Json(HttpStatusCode.OK, """{"deleted":true}"""));
        var nav = new FakeAccountNav();
        var vm = new DeleteAccountViewModel(api, nav, new FakeAppNav()) { Confirm = "delete" };
        await vm.DeleteCommand.ExecuteAsync(null);
        Assert.Equal(0, server.Calls); // lower case is not enough

        vm.Confirm = " DELETE ";
        await vm.DeleteCommand.ExecuteAsync(null);
        Assert.Contains(server.Paths, p => p == "/functions/v1/delete-account");
        Assert.Equal(1, nav.SignedOut);
    }

    [Fact]
    public async Task Failed_delete_keeps_the_user_signed_in()
    {
        var (api, _) = await SignedIn(_ => FakeServer.Json(HttpStatusCode.InternalServerError, """{"error":"DELETE_FAILED"}"""));
        var nav = new FakeAccountNav();
        var vm = new DeleteAccountViewModel(api, nav, new FakeAppNav()) { Confirm = "DELETE" };
        await vm.DeleteCommand.ExecuteAsync(null);
        Assert.Equal(0, nav.SignedOut);
        Assert.NotNull(vm.Error);
    }

    [Fact]
    public async Task Edit_profile_keeps_own_handle_and_existing_photo()
    {
        var (api, server) = await SignedIn(r => r.RequestUri!.AbsolutePath.EndsWith("/profiles") && r.Method == HttpMethod.Get
            ? FakeServer.Json(HttpStatusCode.OK, """[{"handle":"joni","display_name":"Joni","avatar_url":"https://x.test/a.jpg"}]""")
            : r.RequestUri!.AbsolutePath.EndsWith("/user") ? FakeServer.Json(HttpStatusCode.OK, EmailUser)
            : FakeServer.Json(HttpStatusCode.OK, "true"));
        var saved = 0;
        var vm = new ProfileSetupViewModel(new ProfileClient(api, "https://x.test"), new FakeNav(), new FakePicker(null))
            { CheckDelay = TimeSpan.Zero, AfterSave = () => { saved++; return Task.CompletedTask; } };
        await vm.LoadForEditAsync(api.Auth);
        Assert.Equal("joni", vm.Handle);
        Assert.Equal("a@b.co", vm.Email);
        Assert.DoesNotContain(server.Paths, p => p.Contains("is_handle_available")); // own handle is not re-checked

        vm.DisplayName = "Joni K";
        await vm.FinishCommand.ExecuteAsync(null);
        Assert.Equal(1, saved);
        Assert.DoesNotContain(server.Paths, p => p.Contains("/storage/")); // photo unchanged: no upload
    }
}
