using System.Net;
using FridgeChef.Core;
using FridgeChef.Core.Auth;
using FridgeChef.Core.Recipes;
using FridgeChef.Core.Resources;
using FridgeChef.Core.ViewModels;

namespace FridgeChef.Tests;

sealed class FakeAppNav : IAppNavigator
{
    public List<string> Calls = [];
    public IReadOnlyList<string>? PickResult;
    public Task OpenRecipeAsync(Recipe recipe) { Calls.Add("recipe"); return Task.CompletedTask; }
    public Task OpenFridgeAsync() { Calls.Add("fridge"); return Task.CompletedTask; }
    public Task OpenSuggestionsAsync(SuggestRequest request, string subtitle) { Calls.Add("suggestions:" + string.Join(",", request.Ingredients)); return Task.CompletedTask; }
    public Task OpenSuggestionAsync(Suggestion s) { Calls.Add("suggestion"); return Task.CompletedTask; }
    public Task OpenTabAsync(string route) { Calls.Add("tab:" + route); return Task.CompletedTask; }
    public Task BackAsync() { Calls.Add("back"); return Task.CompletedTask; }
    public Task<IReadOnlyList<string>?> PickCuisinesAsync(IReadOnlyList<Cuisine> all, IReadOnlyList<string> selected, int? max) => Task.FromResult(PickResult);
    public Task ShareTextAsync(string title, string text) => Task.CompletedTask;
}

sealed class FakeToast : IToast
{
    public List<(ToastKind Kind, string Title)> Shown = [];
    public void Show(ToastKind kind, string title, string? detail = null, string? action = null, Func<Task>? onAction = null) => Shown.Add((kind, title));
}

[Collection("Language")]
public class FridgeTests
{
    static readonly long Later = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();

    static async Task<Api> SignedIn(FakeServer server)
    {
        var http = new HttpClient(server) { BaseAddress = new Uri("https://x.test/") };
        var auth = new AuthService(new AuthClient(http), new MemoryStore());
        await auth.SetSessionAsync(new Session("a", "r", Later, "u1"));
        return new Api(http, auth);
    }

    static LocalCache TempCache() => new(Path.Combine(Path.GetTempPath(), $"fc-{Guid.NewGuid()}.db3"));

    [Theory]
    [InlineData("  Chicken  Breast ", "chicken breast")]
    [InlineData("ស៊ុត", "ស៊ុត")]
    [InlineData("rice; drop table", null)]
    [InlineData("<b>", null)]
    [InlineData("", null)]
    public void Ingredient_rules_match_the_server(string input, string? expected) => Assert.Equal(expected, FridgeRules.Clean(input));

    [Fact]
    public void Ingredient_longer_than_40_is_refused() => Assert.Null(FridgeRules.Clean(new string('a', 41)));

    static readonly string Countries = """
        [{"code":"KH","name_en":"Cambodia","name_km":"កម្ពុជា","adjective_en":"Cambodian"},
         {"code":"VN","name_en":"Vietnam","name_km":"វៀតណាម","adjective_en":"Vietnamese"},
         {"code":"CN","name_en":"China","name_km":"ចិន","adjective_en":"Chinese"},
         {"code":"JP","name_en":"Japan","name_km":"ជប៉ុន","adjective_en":"Japanese"},
         {"code":"KR","name_en":"South Korea","name_km":"កូរ៉េ","adjective_en":"Korean"}]
        """;

    async Task<(FridgeViewModel vm, FakeAppNav nav, FakeToast toast)> Fridge()
    {
        var api = await SignedIn(new FakeServer(_ => Task.FromResult(FakeServer.Json(HttpStatusCode.OK, Countries))));
        var nav = new FakeAppNav();
        var toast = new FakeToast();
        var vm = new FridgeViewModel(new RecipeClient(api), TempCache(), api.Auth, nav, toast);
        await vm.AppearingAsync();
        return (vm, nav, toast);
    }

    [Fact]
    public async Task Cambodia_is_the_first_cuisine_chip()
    {
        var (vm, _, _) = await Fridge();
        Assert.Equal("KH", vm.CuisineChips[0].Cuisine.Code);
    }

    [Fact]
    public async Task A_fourth_cuisine_is_blocked_with_a_message()
    {
        var (vm, _, toast) = await Fridge();
        foreach (var chip in vm.CuisineChips.Take(4)) vm.ToggleCuisineCommand.Execute(chip);
        Assert.Equal(3, vm.CuisineChips.Count(c => c.IsSelected));
        Assert.Equal("3 / 3", vm.CounterText);
        Assert.Single(toast.Shown);
    }

    [Fact]
    public async Task Find_recipes_needs_two_ingredients()
    {
        var (vm, nav, _) = await Fridge();
        vm.NewIngredient = "eggs";
        vm.AddIngredientCommand.Execute(null);
        await vm.FindRecipesCommand.ExecuteAsync(null);
        Assert.Equal(AppResources.NeedTwo, vm.Message);
        Assert.Empty(nav.Calls);

        vm.AddQuickCommand.Execute("rice");
        await vm.FindRecipesCommand.ExecuteAsync(null);
        Assert.Equal("suggestions:eggs,rice", Assert.Single(nav.Calls));
    }

    [Fact]
    public async Task Same_ingredient_is_added_once_and_bad_text_explained()
    {
        var (vm, _, _) = await Fridge();
        vm.AddQuickCommand.Execute("Eggs");
        vm.AddQuickCommand.Execute("eggs ");
        Assert.Single(vm.Ingredients);
        vm.NewIngredient = "rice;";
        vm.AddIngredientCommand.Execute(null);
        Assert.Equal(AppResources.BadIngredient, vm.Message);
        Assert.Equal("rice;", vm.NewIngredient); // kept so the user can fix it
    }

    // --- Suggestions page states ---------------------------------------------------------------

    const string ThreeIdeas = """
        {"recipes":[
          {"name":"A","description":"a","calories_per_serving":420,"cook_time_min":20,"servings":2,"have":["2 eggs"],"missing":["1 onion"],"steps":["x"]},
          {"name":"B","description":"b","calories_per_serving":300,"cook_time_min":10,"servings":1,"have":["2 eggs"],"missing":[],"steps":["y"]},
          {"name":"C","description":"c","calories_per_serving":500,"cook_time_min":30,"servings":4,"have":["rice"],"missing":[],"steps":["z"]}]}
        """;

    async Task<(SuggestionsViewModel vm, FakeAppNav nav, FakeToast toast)> Suggestions(Func<HttpRequestMessage, Task<HttpResponseMessage>> server)
    {
        var api = await SignedIn(new FakeServer(server));
        var nav = new FakeAppNav();
        var toast = new FakeToast();
        return (new SuggestionsViewModel(new SuggestClient(api), nav, toast), nav, toast);
    }

    static readonly SuggestRequest Request = new() { Ingredients = ["eggs", "rice"], Language = "en" };

    [Fact]
    public async Task Ideas_are_shown()
    {
        var (vm, _, _) = await Suggestions(_ => Task.FromResult(FakeServer.Json(HttpStatusCode.OK, ThreeIdeas)));
        await vm.StartAsync(Request, "From eggs, rice");
        Assert.True(vm.ShowResults);
        Assert.Equal(3, vm.Results.Count);
        Assert.Equal("About 420 kcal / serving", vm.Results[0].CaloriesText); // approximate (brief)
        Assert.Equal("You have 1 of 2", vm.Results[0].HaveText);
    }

    [Fact]
    public async Task More_ideas_sends_the_shown_dishes_and_adds_new_ones()
    {
        string? sent = null;
        var calls = 0;
        var (vm, _, _) = await Suggestions(async r =>
        {
            if (++calls == 2)
            {
                sent = await r.Content!.ReadAsStringAsync();
                return FakeServer.Json(HttpStatusCode.OK, ThreeIdeas.Replace("\"A\"", "\"D\"").Replace("\"B\"", "\"E\"")); // C repeats
            }
            return FakeServer.Json(HttpStatusCode.OK, ThreeIdeas);
        });
        await vm.StartAsync(Request, "");
        Assert.True(vm.CanLoadMore);
        await vm.LoadMoreCommand.ExecuteAsync(null);
        Assert.Contains("\"exclude\":[\"A\",\"B\",\"C\"]", sent);
        Assert.Equal(["A", "B", "C", "D", "E"], vm.Results.Select(x => x.Name)); // the repeated C is not shown twice
        Assert.False(vm.IsLoadingMore);
    }

    [Fact]
    public async Task More_ideas_keeps_the_list_when_the_limit_is_reached()
    {
        var calls = 0;
        var (vm, _, toast) = await Suggestions(_ => Task.FromResult(++calls == 1
            ? FakeServer.Json(HttpStatusCode.OK, ThreeIdeas)
            : FakeServer.Json(HttpStatusCode.TooManyRequests, """{"error":"RATE_LIMIT_USER"}""")));
        await vm.StartAsync(Request, "");
        await vm.LoadMoreCommand.ExecuteAsync(null);
        Assert.True(vm.ShowResults);
        Assert.Equal(3, vm.Results.Count);
        Assert.Equal("That was 10 for today", Assert.Single(toast.Shown).Title);
    }

    [Fact]
    public async Task Daily_limit_shows_the_limit_screen()
    {
        var (vm, _, _) = await Suggestions(_ => Task.FromResult(FakeServer.Json(HttpStatusCode.TooManyRequests, """{"error":"RATE_LIMIT_USER"}""")));
        await vm.StartAsync(Request, "");
        Assert.True(vm.IsLimit);
        Assert.Equal("That was 10 for today", vm.LimitTitle);
    }

    [Fact]
    public async Task Busy_shows_the_busy_screen()
    {
        var (vm, _, _) = await Suggestions(_ => Task.FromResult(FakeServer.Json(HttpStatusCode.ServiceUnavailable, """{"error":"AI_BUSY"}""")));
        await vm.StartAsync(Request, "");
        Assert.True(vm.IsBusy);
    }

    [Fact]
    public async Task No_internet_goes_back_to_the_fridge_with_a_toast()
    {
        var (vm, nav, toast) = await Suggestions(_ => throw new HttpRequestException());
        await vm.StartAsync(Request, "");
        Assert.Equal(["back"], nav.Calls);
        Assert.Equal(ToastKind.Offline, Assert.Single(toast.Shown).Kind);
    }

    [Fact]
    public async Task Cancel_stops_quietly()
    {
        var gate = new TaskCompletionSource<HttpResponseMessage>();
        var (vm, nav, toast) = await Suggestions(_ => gate.Task);
        var running = vm.StartAsync(Request, "");
        await vm.CancelCommand.ExecuteAsync(null);
        await running;
        Assert.Equal(["back"], nav.Calls);
        Assert.Empty(toast.Shown);
    }

    [Fact]
    public async Task A_suggestion_can_be_saved_and_shows_in_Saved()
    {
        var server = new FakeServer(r => Task.FromResult(FakeServer.Json(HttpStatusCode.Created,
            """[{"id":"row1","recipe_id":null,"created_at":"2026-12-02T03:00:00Z","ai_recipe":{"name":"A","description":"a","calories_per_serving":420,"cook_time_min":20,"servings":2,"have":["2 eggs"],"missing":[],"steps":["x"]}}]""")));
        var api = await SignedIn(server);
        var saved = new SavedRecipes(new RecipeClient(api), TempCache(), api.Auth);
        var s = new Suggestion { Name = "A", Description = "a", Calories = 420, Have = ["2 eggs"], Steps = ["x"] };
        var vm = new SuggestionDetailViewModel(saved, new FakeAppNav(), new FakeToast());
        vm.Show(s);
        await vm.ToggleSaveCommand.ExecuteAsync(null);
        Assert.True(vm.IsSaved);
        var item = Assert.Single(saved.Items);
        Assert.True(item.IsAi);
        Assert.Equal("A", item.Recipe!.TitleEn);         // same card as recipes in the Saved tab
        Assert.Equal("About 420 kcal / serving", item.Recipe.CaloriesText);
    }
}
