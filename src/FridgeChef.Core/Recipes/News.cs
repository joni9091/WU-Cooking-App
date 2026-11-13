using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FridgeChef.Core.Auth;
using FridgeChef.Core.ViewModels;
using FridgeChef.Core.Resources;

namespace FridgeChef.Core.Recipes;

// One food news story (filled hourly by the refresh-news function). Stays in its original language (brief).
public sealed class NewsItem
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("title_en")] public string Title { get; set; } = "";
    [JsonPropertyName("summary_en")] public string? Summary { get; set; }
    [JsonPropertyName("source")] public string? Source { get; set; }
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("image_url")] public string? ImageUrl { get; set; }
    [JsonPropertyName("published_at")] public DateTimeOffset PublishedAt { get; set; }

    public const string Columns = "id,title_en,summary_en,source,url,image_url,published_at";

    [JsonIgnore] public string MetaText => $"{Source} · {Format.Date(PublishedAt)}"; // "The Guardian · 12 Nov 2026"
    [JsonIgnore] public IReadOnlyList<string> Paragraphs => (Summary ?? "").Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
    [JsonIgnore] public bool HasImage => !string.IsNullOrEmpty(ImageUrl);
    // Only https links leave the app for the browser.
    [JsonIgnore] public bool HasSafeLink => Uri.TryCreate(Url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps;
}

public sealed class NewsClient(Api api)
{
    public async Task<List<NewsItem>> GetAsync(int offset, int limit = RecipeQuery.PageSize)
    {
        using var res = await api.SendAsync(HttpMethod.Get,
            $"rest/v1/news?select={NewsItem.Columns}&is_published=eq.true&order=published_at.desc,id.asc&limit={limit}&offset={offset}");
        return await res.Content.ReadFromJsonAsync(NewsJson.Default.ListNewsItem) ?? [];
    }
}

[JsonSerializable(typeof(List<NewsItem>))]
partial class NewsJson : JsonSerializerContext;

public interface INewsNavigator
{
    Task OpenArticleAsync(NewsItem item);
    Task OpenInBrowserAsync(Uri url);
}

// Figma 7. News: newest story large on top ("Latest"), the rest as a list, 20 per page, phone copy first.
public sealed partial class NewsViewModel(NewsClient client, LocalCache cache, INewsNavigator nav, IToast toast) : ObservableObject
{
    bool _started, _hasMore = true;
    int _version;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFeatured))]
    public partial NewsItem? Featured { get; set; }
    public bool HasFeatured => Featured is not null;
    public System.Collections.ObjectModel.ObservableCollection<NewsItem> Items { get; } = [];
    [ObservableProperty] public partial bool IsRefreshing { get; set; }
    [ObservableProperty] public partial bool IsLoading { get; set; } = true;

    public async Task AppearingAsync()
    {
        if (_started) return;
        _started = true;
        if (await cache.GetAsync("news", NewsJson.Default.ListNewsItem) is { Count: > 0 } cached) { Show(cached); IsLoading = false; }
        await Refresh();
    }

    void Show(List<NewsItem> first)
    {
        Featured = first.FirstOrDefault();
        Items.Clear();
        foreach (var n in first.Skip(1)) Items.Add(n);
    }

    [RelayCommand]
    async Task Refresh()
    {
        var version = ++_version;
        try
        {
            var page = await client.GetAsync(0);
            if (version != _version) return;
            Show(page);
            _hasMore = page.Count == RecipeQuery.PageSize;
            await cache.PutAsync("news", page, NewsJson.Default.ListNewsItem);
        }
        catch (AuthException e) { Errors.Show(toast, e); }
        finally { IsRefreshing = false; IsLoading = false; }
    }

    [RelayCommand]
    async Task LoadMore()
    {
        if (!_hasMore || IsLoading || Featured is null) return;
        var version = _version;
        IsLoading = true;
        try
        {
            var page = await client.GetAsync(Items.Count + 1);
            if (version != _version) return;
            foreach (var n in page) Items.Add(n);
            _hasMore = page.Count == RecipeQuery.PageSize;
        }
        catch (AuthException e) { Errors.Show(toast, e); }
        finally { IsLoading = false; }
    }

    [RelayCommand] Task Open(NewsItem item) => nav.OpenArticleAsync(item);
}

// Figma 16. News article: photo, title, source and date, summary, "Open original article".
public sealed partial class NewsArticleViewModel(INewsNavigator nav, IAppNavigator app) : ObservableObject
{
    [ObservableProperty] public partial NewsItem? Item { get; set; }

    [RelayCommand]
    Task OpenOriginal() => Item is { HasSafeLink: true } n ? nav.OpenInBrowserAsync(new Uri(n.Url)) : Task.CompletedTask;

    // Figma has a share button: shares the headline and the link.
    [RelayCommand]
    Task Share() => Item is { } n ? app.ShareTextAsync(n.Title, $"{n.Title}\n{n.Url}") : Task.CompletedTask;

    [RelayCommand] Task Back() => app.BackAsync();
}
