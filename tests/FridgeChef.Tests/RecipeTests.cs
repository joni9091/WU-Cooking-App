using System.Globalization;
using System.Net;
using FridgeChef.Core;
using FridgeChef.Core.Auth;
using FridgeChef.Core.Recipes;
using FridgeChef.Core.Resources;
using FridgeChef.Core.ViewModels;

namespace FridgeChef.Tests;

// AppResources.Culture is global, so tests that switch language don't run in parallel with each other.
[Collection("Language")]
public class RecipeTests : IDisposable
{
    public RecipeTests() => AppResources.Culture = new CultureInfo("en-US");
    public void Dispose() => AppResources.Culture = new CultureInfo("en-US");

    static void Khmer() => AppResources.Culture = new CultureInfo("km-KH");

    static readonly DateTimeOffset Utc = new(2026, 12, 1, 20, 30, 0, TimeSpan.Zero);
    static readonly TimeZoneInfo PhnomPenh = TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "ICT", "ICT");

    [Fact]
    public void Date_is_day_short_month_year_in_local_time() =>
        Assert.Equal("2 Dec 2026", Format.Date(Utc, PhnomPenh)); // 20:30 UTC on 1 Dec is already 2 Dec in Cambodia

    [Fact]
    public void Khmer_date_uses_Khmer_month_and_numerals()
    {
        Khmer();
        Assert.Equal("២ ធ្នូ ២០២៦", Format.Date(Utc, PhnomPenh));
    }

    [Fact]
    public void Calories_in_both_languages()
    {
        Assert.Equal("420 kcal / serving", Format.Kcal(420));
        Khmer();
        Assert.Equal("៤២០ kcal / ចំណែក", Format.Kcal(420));
    }

    [Fact]
    public void Missing_Khmer_text_falls_back_to_English()
    {
        var r = new Recipe { TitleEn = "Fish Amok", TitleKm = "", StepsEn = ["Cut.", "Steam."], StepsKm = [] };
        Khmer();
        Assert.Equal("Fish Amok", r.Title);
        Assert.Equal(["Cut.", "Steam."], r.Steps.Select(s => s.Text));
        Assert.Equal("២", r.Steps[1].Number);
    }

    [Fact]
    public void Khmer_text_is_used_when_present()
    {
        var r = new Recipe { TitleEn = "Fish Amok", TitleKm = "អាម៉ុកត្រី" };
        Khmer();
        Assert.Equal("អាម៉ុកត្រី", r.Title);
    }

    [Theory]
    [InlineData("amok", "amok")]
    [InlineData("a,b)*(c", "abc")]               // filter syntax characters removed
    [InlineData("  fish  amok ", "fish  amok")]
    [InlineData("អាម៉ុក", "អាម៉ុក")]              // Khmer letters and marks kept
    public void Search_text_is_cleaned(string input, string expected) => Assert.Equal(expected, RecipeQuery.CleanSearch(input));

    [Fact]
    public void Search_is_capped() => Assert.Equal(RecipeQuery.MaxSearchLength, RecipeQuery.CleanSearch(new string('a', 100)).Length);

    [Fact]
    public void Query_path_has_paging_filters_and_stable_order()
    {
        var path = new RecipeQuery("amok", "soup", ["KH", "VN"], Offset: 20).ToPath();
        Assert.Contains("order=popularity.desc,id.asc", path);
        Assert.Contains("limit=20&offset=20", path);
        Assert.Contains("category=eq.soup", path);
        Assert.Contains("cuisine=in.(KH,VN)", path);
        Assert.Contains("or=(title_en.ilike.%2Aamok%2A,title_km.ilike.%2Aamok%2A)", path);
    }

    [Fact]
    public void Empty_search_adds_no_filter() => Assert.DoesNotContain("or=", new RecipeQuery("  ,*  ").ToPath());

    [Fact]
    public async Task Local_cache_keeps_lists_between_launches()
    {
        var file = Path.Combine(Path.GetTempPath(), $"fc-{Guid.NewGuid()}.db3");
        try
        {
            await new LocalCache(file).PutAsync("home.popular", [new Recipe { Id = "1", TitleEn = "Lok Lak" }], RecipeJson.Default.ListRecipe);
            var again = await new LocalCache(file).GetAsync("home.popular", RecipeJson.Default.ListRecipe);
            Assert.Equal("Lok Lak", Assert.Single(again!).TitleEn);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public async Task Saved_lists_are_kept_per_user_on_the_phone()
    {
        var file = Path.Combine(Path.GetTempPath(), $"fc-{Guid.NewGuid()}.db3");
        try
        {
            var cache = new LocalCache(file);
            var server = new FakeServer(_ => Task.FromResult(FakeServer.Json(HttpStatusCode.Created, "")));
            var http = new HttpClient(server) { BaseAddress = new Uri("https://x.test/") };

            var authA = new AuthService(new AuthClient(http), new MemoryStore());
            await authA.SetSessionAsync(new Session("a", "r", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), "userA"));
            await new SavedRecipes(new RecipeClient(new Api(http, authA)), cache, authA).SaveAsync(new Recipe { Id = "r1", TitleEn = "Fish Amok" });

            var authB = new AuthService(new AuthClient(http), new MemoryStore());
            await authB.SetSessionAsync(new Session("b", "r", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), "userB"));
            var savedB = new SavedRecipes(new RecipeClient(new Api(http, authB)), cache, authB);
            await savedB.LoadCachedAsync();
            Assert.Empty(savedB.Items); // user B never sees user A's saved recipes
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public async Task Saving_offline_changes_nothing()
    {
        var http = new HttpClient(new FakeServer(_ => throw new HttpRequestException())) { BaseAddress = new Uri("https://x.test/") };
        var auth = new AuthService(new AuthClient(http), new MemoryStore());
        await auth.SetSessionAsync(new Session("a", "r", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), "u"));
        var saved = new SavedRecipes(new RecipeClient(new Api(http, auth)), new LocalCache(Path.Combine(Path.GetTempPath(), $"fc-{Guid.NewGuid()}.db3")), auth);
        var e = await Assert.ThrowsAsync<AuthException>(() => saved.SaveAsync(new Recipe { Id = "r1" }));
        Assert.Equal(AuthException.Network, e.Code);
        Assert.False(saved.IsSaved("r1"));
    }

    static IReadOnlyList<Cuisine> Countries() =>
        [.. new[] { "KH", "VN", "LA", "CN" }.Select(c => new Cuisine { Code = c, NameEn = c, NameKm = c })];

    [Fact]
    public void Picker_blocks_a_fourth_cuisine_on_the_Fridge_page()
    {
        var vm = new CuisinePickerViewModel(Countries(), ["KH", "VN", "LA"], max: 3);
        vm.ToggleCommand.Execute(vm.Visible[3]);
        Assert.False(vm.Visible[3].IsChecked);
        Assert.NotNull(vm.Message);
        Assert.Equal("3 / 3", vm.CounterText);
    }

    [Fact]
    public void Recipe_filter_has_no_limit()
    {
        var vm = new CuisinePickerViewModel(Countries(), ["KH", "VN", "LA"], max: null);
        vm.ToggleCommand.Execute(vm.Visible[3]);
        Assert.Equal(4, vm.SelectedCodes.Count);
        Assert.False(vm.ShowCounter);
    }

    [Fact]
    public void Picker_search_filters_countries()
    {
        var vm = new CuisinePickerViewModel(Countries(), [], max: null) { Search = "v" };
        Assert.Equal("VN", Assert.Single(vm.Visible).Cuisine.Code);
    }
}
