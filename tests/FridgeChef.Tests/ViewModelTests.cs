using System.Net;
using FridgeChef.Core.Auth;
using FridgeChef.Core.Resources;
using FridgeChef.Core.ViewModels;

namespace FridgeChef.Tests;

[Collection("Language")] // reads translated text; must not overlap with tests that switch language
public class ViewModelTests
{
    static readonly long Later = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();

    static AuthService Auth(FakeServer server) =>
        new(new AuthClient(new HttpClient(server) { BaseAddress = new Uri("https://x.test/") }), new MemoryStore());

    static FakeServer Answer(HttpStatusCode code, string body) => new(_ => Task.FromResult(FakeServer.Json(code, body)));

    static FakeServer NoServer() => new(_ => throw new InvalidOperationException("no request expected"));

    [Fact]
    public async Task Login_checks_the_email_before_calling_the_server()
    {
        var server = NoServer();
        var vm = new LoginViewModel(Auth(server), new FakeNav(), new FakeWeb(null)) { Email = "not-an-email", Password = "12345678" };
        await vm.SignInCommand.ExecuteAsync(null);
        Assert.Equal(AppResources.ErrEmail, vm.Error);
        Assert.Equal(0, server.Calls);
    }

    [Fact]
    public async Task Login_wrong_password_shows_a_friendly_message()
    {
        var nav = new FakeNav();
        var vm = new LoginViewModel(Auth(Answer(HttpStatusCode.BadRequest, """{"error_code":"invalid_credentials"}""")), nav, new FakeWeb(null))
            { Email = "a@b.co", Password = "12345678" };
        await vm.SignInCommand.ExecuteAsync(null);
        Assert.Equal(AppResources.ErrLogin, vm.Error);
        Assert.Equal(0, nav.SignedIn);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Login_success_saves_the_session_and_moves_on()
    {
        var nav = new FakeNav();
        var auth = Auth(Answer(HttpStatusCode.OK, AuthServiceTests.Token("a1", Later)));
        var vm = new LoginViewModel(auth, nav, new FakeWeb(null)) { Email = "a@b.co", Password = "12345678" };
        await vm.SignInCommand.ExecuteAsync(null);
        Assert.Null(vm.Error);
        Assert.True(auth.IsSignedIn);
        Assert.Equal(1, nav.SignedIn);
        Assert.Equal("", vm.Password); // not kept in memory
    }

    [Fact]
    public async Task No_internet_says_so()
    {
        var vm = new LoginViewModel(Auth(new FakeServer(_ => throw new HttpRequestException())), new FakeNav(), new FakeWeb(null))
            { Email = "a@b.co", Password = "12345678" };
        await vm.SignInCommand.ExecuteAsync(null);
        Assert.Equal(AppResources.ErrNoInternet, vm.Error);
    }

    [Fact]
    public async Task Sign_up_rejects_mismatched_passwords()
    {
        var vm = new SignUpViewModel(Auth(NoServer()), new FakeNav(), new FakeWeb(null)) { Email = "a@b.co", Password = "12345678", Confirm = "87654321" };
        await vm.SignUpCommand.ExecuteAsync(null);
        Assert.Equal(AppResources.ErrPasswordMatch, vm.Error);
    }

    [Fact]
    public async Task Sign_up_rejects_short_passwords()
    {
        var vm = new SignUpViewModel(Auth(NoServer()), new FakeNav(), new FakeWeb(null)) { Email = "a@b.co", Password = "1234567", Confirm = "1234567" };
        await vm.SignUpCommand.ExecuteAsync(null);
        Assert.Equal(AppResources.ErrPassword, vm.Error);
    }

    [Fact]
    public async Task Sign_up_success_asks_to_check_email()
    {
        var vm = new SignUpViewModel(Auth(Answer(HttpStatusCode.OK, """{"id":"u1"}""")), new FakeNav(), new FakeWeb(null))
            { Email = "a@b.co", Password = "12345678", Confirm = "12345678" };
        await vm.SignUpCommand.ExecuteAsync(null);
        Assert.Null(vm.Error);
        Assert.Equal(AppResources.SignUpDone, vm.Info);
    }

    [Fact]
    public async Task Closing_the_Google_window_is_not_an_error()
    {
        var server = NoServer();
        var nav = new FakeNav();
        var vm = new LoginViewModel(Auth(server), nav, new FakeWeb(null));
        await vm.GoogleCommand.ExecuteAsync(null);
        Assert.Null(vm.Error);
        Assert.Equal(0, nav.SignedIn);
        Assert.Equal(0, server.Calls);
    }

    [Fact]
    public async Task Google_code_is_exchanged_for_a_session()
    {
        var server = Answer(HttpStatusCode.OK, AuthServiceTests.Token("g1", Later));
        var nav = new FakeNav();
        var auth = Auth(server);
        await new LoginViewModel(auth, nav, new FakeWeb("the-code")).GoogleCommand.ExecuteAsync(null);
        Assert.Contains(server.Paths, p => p.Contains("grant_type=pkce"));
        Assert.True(auth.IsSignedIn);
        Assert.Equal(1, nav.SignedIn);
    }

    [Fact]
    public async Task Reset_code_screen_appears_without_revealing_the_account()
    {
        var vm = new ForgotPasswordViewModel(Auth(Answer(HttpStatusCode.OK, "{}")), new FakeNav(), new FakeWeb(null)) { Email = "a@b.co" };
        await vm.SendCodeCommand.ExecuteAsync(null);
        Assert.True(vm.CodeSent);
        Assert.Null(vm.Error);
    }

    [Fact]
    public async Task Wrong_reset_code_is_explained()
    {
        var vm = new ForgotPasswordViewModel(Auth(Answer(HttpStatusCode.Forbidden, """{"error_code":"otp_expired"}""")), new FakeNav(), new FakeWeb(null))
            { Email = "a@b.co", Code = "123456", NewPassword = "12345678" };
        await vm.SavePasswordCommand.ExecuteAsync(null);
        Assert.Equal(AppResources.ErrCodeWrong, vm.Error);
    }

    // Profile setup ------------------------------------------------------------

    static async Task<(ProfileSetupViewModel vm, FakeNav nav, FakeServer server)> Profile(Func<HttpRequestMessage, HttpResponseMessage> answer, byte[]? photo = null)
    {
        var server = new FakeServer(r => Task.FromResult(answer(r)));
        var auth = Auth(server);
        await auth.SetSessionAsync(new Session("a1", "r1", Later, "u1"));
        var nav = new FakeNav();
        var profiles = new ProfileClient(new Api(new HttpClient(server) { BaseAddress = new Uri("https://x.test/") }, auth), "https://x.test");
        return (new ProfileSetupViewModel(profiles, nav, new FakePicker(photo)) { CheckDelay = TimeSpan.Zero }, nav, server);
    }

    [Fact]
    public async Task Handle_is_lower_cased_while_typing()
    {
        var (vm, _, _) = await Profile(_ => FakeServer.Json(HttpStatusCode.OK, "true"));
        vm.Handle = "Joni.Cooks";
        Assert.Equal("joni.cooks", vm.Handle);
    }

    [Fact]
    public async Task Characters_not_allowed_in_a_handle_cannot_be_typed()
    {
        var (vm, _, _) = await Profile(_ => FakeServer.Json(HttpStatusCode.OK, "true"));
        vm.Handle = "@Joni-Test x_1";
        Assert.Equal("jonitestx_1", vm.Handle);
    }

    [Fact]
    public async Task Taken_handle_is_reported_while_typing()
    {
        var (vm, _, _) = await Profile(_ => FakeServer.Json(HttpStatusCode.OK, "false"));
        vm.Handle = "joni";
        for (var i = 0; i < 50 && vm.HandleStatus != AppResources.HandleTaken; i++) await Task.Delay(10);
        Assert.Equal(AppResources.HandleTaken, vm.HandleStatus);
    }

    [Fact]
    public async Task Finish_requires_a_name()
    {
        var (vm, nav, _) = await Profile(_ => FakeServer.Json(HttpStatusCode.OK, "true"));
        vm.Handle = "joni";
        await vm.FinishCommand.ExecuteAsync(null);
        Assert.Equal(AppResources.ErrName, vm.Error);
        Assert.Equal(0, nav.SignedIn);
    }

    [Fact]
    public async Task Finish_reports_a_handle_taken_at_the_last_moment()
    {
        var (vm, nav, _) = await Profile(r => r.Method == HttpMethod.Patch
            ? FakeServer.Json(HttpStatusCode.Conflict, """{"code":"23505"}""")
            : FakeServer.Json(HttpStatusCode.OK, "true"));
        vm.Handle = "joni"; vm.DisplayName = "Joni";
        await vm.FinishCommand.ExecuteAsync(null);
        Assert.Equal(AppResources.HandleTaken, vm.HandleStatus); // shown under the handle box
        Assert.True(vm.HandleIsBad);
        Assert.Equal(0, nav.SignedIn);
    }

    [Fact]
    public async Task Finish_uploads_the_photo_into_the_users_own_folder_then_saves()
    {
        var (vm, nav, server) = await Profile(_ => FakeServer.Json(HttpStatusCode.OK, "true"), photo: [1, 2, 3]);
        await vm.PickPhotoCommand.ExecuteAsync(null);
        vm.Handle = "joni"; vm.DisplayName = "Joni";
        await vm.FinishCommand.ExecuteAsync(null);
        Assert.Null(vm.Error);
        Assert.Contains(server.Paths, p => p == "/storage/v1/object/avatars/u1/avatar.jpg");
        Assert.Contains(server.Paths, p => p.StartsWith("/rest/v1/profiles?id=eq.u1"));
        Assert.Equal(1, nav.SignedIn);
    }
}
