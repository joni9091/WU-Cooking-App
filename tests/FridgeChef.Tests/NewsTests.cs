using System.Net;
using FridgeChef.Core.Auth;
using FridgeChef.Core.Recipes;

namespace FridgeChef.Tests;

sealed class FakeNewsNav : INewsNavigator
{
    public List<Uri> Opened = [];
    public Task OpenArticleAsync(NewsItem item) => Task.CompletedTask;
    public Task OpenInBrowserAsync(Uri url) { Opened.Add(url); return Task.CompletedTask; }
}

[Collection("Language")]
public class NewsTests
{
    [Fact]
    public void Meta_line_and_paragraphs()
    {
        var n = new NewsItem { Source = "The Guardian", PublishedAt = new DateTimeOffset(2026, 11, 12, 12, 0, 0, TimeSpan.Zero), Summary = "One.\n\nTwo." };
        Assert.Equal("The Guardian · 12 Nov 2026", n.MetaText);
        Assert.Equal(["One.", "Two."], n.Paragraphs);
    }

    [Theory]
    [InlineData("https://www.theguardian.com/food/x", true)]
    [InlineData("http://example.com", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("", false)]
    public void Only_https_links_open_the_browser(string url, bool opens)
    {
        var nav = new FakeNewsNav();
        var vm = new NewsArticleViewModel(nav, new FakeAppNav()) { Item = new NewsItem { Url = url } };
        vm.OpenOriginalCommand.Execute(null);
        Assert.Equal(opens, nav.Opened.Count == 1);
    }

    [Fact]
    public async Task Newest_story_is_featured_and_the_rest_are_listed()
    {
        var http = new HttpClient(new FakeServer(_ => Task.FromResult(FakeServer.Json(HttpStatusCode.OK,
            """[{"id":"1","title_en":"New","url":"https://a","published_at":"2026-11-13T00:00:00Z"},{"id":"2","title_en":"Old","url":"https://b","published_at":"2026-11-12T00:00:00Z"}]"""))))
            { BaseAddress = new Uri("https://x.test/") };
        var auth = new AuthService(new AuthClient(http), new MemoryStore());
        await auth.SetSessionAsync(new Session("a", "r", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), "u"));
        var vm = new NewsViewModel(new NewsClient(new Api(http, auth)), new LocalCache(Path.Combine(Path.GetTempPath(), $"fc-{Guid.NewGuid()}.db3")), new FakeNewsNav(), new FakeToast());
        await vm.AppearingAsync();
        Assert.Equal("New", vm.Featured!.Title);
        Assert.Equal("Old", Assert.Single(vm.Items).Title);
    }
}
