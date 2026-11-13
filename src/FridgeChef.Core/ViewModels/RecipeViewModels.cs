using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FridgeChef.Core.Auth;
using FridgeChef.Core.Recipes;
using FridgeChef.Core.Resources;

namespace FridgeChef.Core.ViewModels;

// Phone features the recipe screens need (the app provides them; tests use fakes).
public interface IAppNavigator
{
    Task OpenRecipeAsync(Recipe recipe);
    Task OpenFridgeAsync();
    Task OpenSuggestionsAsync(SuggestRequest request, string subtitle);
    Task OpenSuggestionAsync(Suggestion suggestion);
    Task OpenTabAsync(string route);   // "recipe", "account"
    Task BackAsync();
    // Country picker sheet; max = null for the Recipe filter, 3 for the Fridge page. Null result = cancelled.
    Task<IReadOnlyList<string>?> PickCuisinesAsync(IReadOnlyList<Cuisine> all, IReadOnlyList<string> selected, int? max);
    Task ShareTextAsync(string title, string text);
}

public enum ToastKind { Success, Offline, Removed, Info }

public interface IToast
{
    void Show(ToastKind kind, string title, string? detail = null, string? action = null, Func<Task>? onAction = null);
}

static class Errors
{
    // Network problems become the "No internet" toast; anything else a generic one.
    public static void Show(IToast toast, AuthException e)
    {
        if (e.Code == AuthException.Network) toast.Show(ToastKind.Offline, AppResources.NoInternetTitle, AppResources.NoInternetSub);
        else toast.Show(ToastKind.Info, AppResources.ErrGeneric);
    }
}

// Home tab: trending carousel + popular list. Phone copy first, then the server.
public sealed partial class HomeViewModel(RecipeClient client, LocalCache cache, IAppNavigator nav, IToast toast) : ObservableObject
{
    bool _started;

    [ObservableProperty] public partial IReadOnlyList<Recipe> Trending { get; set; } = [];
    [ObservableProperty] public partial IReadOnlyList<Recipe> Popular { get; set; } = [];
    [ObservableProperty] public partial bool IsRefreshing { get; set; }
    [ObservableProperty] public partial bool IsLoading { get; set; } = true; // skeleton until the first data

    public async Task AppearingAsync()
    {
        if (_started) return;
        _started = true;
        if (await cache.GetAsync("home.trending", RecipeJson.Default.ListRecipe) is { } t) Trending = t;
        if (await cache.GetAsync("home.popular", RecipeJson.Default.ListRecipe) is { } p) Popular = p;
        IsLoading = Popular.Count == 0;
        await Refresh();
    }

    [RelayCommand]
    async Task Refresh()
    {
        try
        {
            var trending = client.GetAsync(new RecipeQuery(TrendingOnly: true, Limit: 10));
            var popular = client.GetAsync(new RecipeQuery());
            Trending = await trending;
            Popular = [.. (await popular).Where(r => !r.IsTrending)]; // Figma: trending dishes are in the carousel only
            await cache.PutAsync("home.trending", [.. Trending], RecipeJson.Default.ListRecipe);
            await cache.PutAsync("home.popular", [.. Popular], RecipeJson.Default.ListRecipe);
        }
        catch (AuthException e) { Errors.Show(toast, e); }
        finally { IsRefreshing = false; IsLoading = false; }
    }

    [RelayCommand] Task Open(Recipe recipe) => nav.OpenRecipeAsync(recipe);
    [RelayCommand] Task GoTo(string route) => nav.OpenTabAsync(route);
}

public sealed partial class CategoryChip(string key, string label) : ObservableObject
{
    public string Key => key;       // "" = All
    public string Label => label;
    [ObservableProperty] public partial bool IsSelected { get; set; }
}

// Recipe tab: search + country filter + category chips + grid, 20 recipes per page.
public sealed partial class RecipesViewModel(RecipeClient client, LocalCache cache, IAppNavigator nav, IToast toast) : ObservableObject
{
    public TimeSpan SearchDelay { get; init; } = TimeSpan.FromMilliseconds(400);
    int _version;              // drops answers to old searches that arrive late
    bool _hasMore = true, _started;
    CancellationTokenSource? _typing;
    List<Cuisine>? _cuisines;

    public ObservableCollection<Recipe> Items { get; } = [];
    public ObservableCollection<Cuisine> SelectedCuisines { get; } = [];
    public IReadOnlyList<CategoryChip> Categories { get; } =
    [
        new("", AppResources.CatAll) { IsSelected = true }, new("soup", AppResources.CatSoup), new("rice", AppResources.CatRice),
        new("sweet", AppResources.CatSweet), new("drinks", AppResources.CatDrinks), new("noodles", AppResources.CatNoodles),
        new("curry", AppResources.CatCurry), new("main", AppResources.CatMain), new("salad", AppResources.CatSalad),
        new("snack", AppResources.CatSnack),
    ];

    [ObservableProperty] public partial string Search { get; set; } = "";
    [ObservableProperty] public partial bool IsRefreshing { get; set; }
    [ObservableProperty] public partial bool IsLoading { get; set; } = true;
    [ObservableProperty] public partial bool IsEmpty { get; set; }

    string Category => Categories.First(c => c.IsSelected).Key;
    public bool HasCuisines => SelectedCuisines.Count > 0;

    public async Task AppearingAsync()
    {
        if (_started) return;
        _started = true;
        await ReloadAsync();
    }

    partial void OnSearchChanged(string value)
    {
        _typing?.Cancel();
        _typing = new CancellationTokenSource();
        _ = SearchSoonAsync(_typing.Token);
    }

    async Task SearchSoonAsync(CancellationToken ct)
    {
        try { await Task.Delay(SearchDelay, ct); await ReloadAsync(); }
        catch (OperationCanceledException) { } // still typing
    }

    [RelayCommand]
    async Task SelectCategory(CategoryChip chip)
    {
        foreach (var c in Categories) c.IsSelected = c == chip;
        await ReloadAsync();
    }

    [RelayCommand]
    async Task Filter()
    {
        try { _cuisines ??= await CuisinesAsync(); }
        catch (AuthException e) { Errors.Show(toast, e); return; }
        var picked = await nav.PickCuisinesAsync(_cuisines, [.. SelectedCuisines.Select(c => c.Code)], max: null);
        if (picked is null) return;
        SelectedCuisines.Clear();
        foreach (var c in _cuisines.Where(c => picked.Contains(c.Code))) SelectedCuisines.Add(c); // keeps Cambodia-first order
        await ReloadAsync();
    }

    [RelayCommand]
    async Task RemoveCuisine(Cuisine cuisine)
    {
        SelectedCuisines.Remove(cuisine);
        await ReloadAsync();
    }

    [RelayCommand] Task Refresh() => ReloadAsync();

    [RelayCommand]
    async Task LoadMore()
    {
        if (!_hasMore || IsLoading || Items.Count == 0) return;
        await LoadPageAsync(_version, Items.Count);
    }

    [RelayCommand] Task Open(Recipe recipe) => nav.OpenRecipeAsync(recipe);

    [RelayCommand] Task Fridge() => nav.OpenFridgeAsync();

    async Task ReloadAsync()
    {
        OnPropertyChanged(nameof(HasCuisines));
        var version = ++_version;
        _hasMore = true;
        Items.Clear();
        await LoadPageAsync(version, 0);
    }

    async Task LoadPageAsync(int version, int offset)
    {
        IsLoading = true;
        try
        {
            var page = await client.GetAsync(new RecipeQuery(Search, Category, [.. SelectedCuisines.Select(c => c.Code)], Offset: offset));
            if (version != _version) return; // the user changed the search meanwhile
            foreach (var r in page) Items.Add(r);
            _hasMore = page.Count == RecipeQuery.PageSize;
        }
        catch (AuthException e) { if (version == _version) Errors.Show(toast, e); }
        finally
        {
            if (version == _version) { IsLoading = false; IsRefreshing = false; IsEmpty = Items.Count == 0; }
        }
    }

    async Task<List<Cuisine>> CuisinesAsync()
    {
        try
        {
            var fresh = await client.GetCuisinesAsync();
            await cache.PutAsync("cuisines", fresh, RecipeJson.Default.ListCuisine);
            return fresh;
        }
        catch (AuthException)
        {
            // offline: last known list, if any
            if (await cache.GetAsync("cuisines", RecipeJson.Default.ListCuisine) is { Count: > 0 } cached) return cached;
            throw;
        }
    }
}

// Recipe detail: everything about one recipe, Save and Share.
public sealed partial class RecipeDetailViewModel(SavedRecipes saved, IAppNavigator nav, IToast toast) : ObservableObject
{
    [ObservableProperty] public partial Recipe? Recipe { get; set; }
    [ObservableProperty] public partial bool IsSaved { get; set; }
    [ObservableProperty] public partial string? SavedOnText { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }

    public string SaveText => IsSaved ? AppResources.SavedButton : AppResources.SaveRecipe;

    public void Show(Recipe recipe)
    {
        Recipe = recipe;
        UpdateSaved();
    }

    void UpdateSaved()
    {
        var item = Recipe is null ? null : saved.Find(Recipe.Id);
        IsSaved = item is not null;
        SavedOnText = item?.SavedOnText;
        OnPropertyChanged(nameof(SaveText));
    }

    [RelayCommand]
    async Task ToggleSave()
    {
        if (Recipe is null || IsBusy) return;
        IsBusy = true;
        try
        {
            if (IsSaved)
            {
                await saved.RemoveAsync(Recipe.Id);
                toast.Show(ToastKind.Removed, AppResources.RemovedToast);
            }
            else
            {
                await saved.SaveAsync(Recipe);
                toast.Show(ToastKind.Success, AppResources.RecipeSavedToast, saved.Find(Recipe.Id)?.SavedOnText);
            }
        }
        catch (AuthException e) { Errors.Show(toast, e); }
        finally { IsBusy = false; UpdateSaved(); }
    }

    [RelayCommand]
    Task Share()
    {
        if (Recipe is not { } r) return Task.CompletedTask;
        var text = new StringBuilder()
            .AppendLine(r.Title).AppendLine(r.CaloriesText).AppendLine()
            .AppendLine(r.Description).AppendLine()
            .AppendLine(AppResources.Ingredients);
        foreach (var i in r.Ingredients) text.AppendLine("• " + i);
        text.AppendLine().AppendLine(AppResources.Steps);
        foreach (var s in r.Steps) text.AppendLine($"{s.Number}. {s.Text}");
        return nav.ShareTextAsync(r.Title, text.ToString());
    }

    [RelayCommand] Task Back() => nav.BackAsync();
}

// Saved tab: newest first, works offline from the phone copy.
public sealed partial class SavedViewModel(SavedRecipes saved, IAppNavigator nav, IToast toast) : ObservableObject
{
    [ObservableProperty] public partial IReadOnlyList<SavedItem> Items { get; set; } = [];
    [ObservableProperty] public partial bool IsRefreshing { get; set; }
    [ObservableProperty] public partial bool IsLoading { get; set; } = true;
    [ObservableProperty] public partial bool IsEmpty { get; set; }

    public async Task AppearingAsync()
    {
        saved.Changed -= Update;
        saved.Changed += Update;
        await saved.LoadCachedAsync();
        Update();
        await Refresh();
    }

    void Update()
    {
        Items = saved.Items;
        IsEmpty = !IsLoading && Items.Count == 0;
    }

    [RelayCommand]
    async Task Refresh()
    {
        try { await saved.RefreshAsync(); }
        catch (AuthException e) { if (saved.Items.Count == 0 || e.Code != AuthException.Network) Errors.Show(toast, e); }
        finally { IsRefreshing = false; IsLoading = false; Update(); }
    }

    [RelayCommand]
    async Task Remove(SavedItem item)
    {
        try
        {
            await saved.RemoveItemAsync(item);
            toast.Show(ToastKind.Removed, AppResources.RemovedToast, null, AppResources.Undo,
                item.IsAi ? () => saved.SaveSuggestionAsync(item.Ai!) : () => saved.SaveAsync(item.Recipe!));
        }
        catch (AuthException e) { Errors.Show(toast, e); }
    }

    [RelayCommand] Task Open(SavedItem item) => item.IsAi ? nav.OpenSuggestionAsync(item.Ai!) : nav.OpenRecipeAsync(item.Recipe!);
}

public sealed partial class CuisineOption(Cuisine cuisine) : ObservableObject
{
    public Cuisine Cuisine => cuisine;
    [ObservableProperty] public partial bool IsChecked { get; set; }
}

// Country picker sheet (Figma 28). Max 3 on the Fridge page; no limit for the Recipe filter.
public sealed partial class CuisinePickerViewModel : ObservableObject
{
    readonly List<CuisineOption> _all;

    public CuisinePickerViewModel(IReadOnlyList<Cuisine> all, IReadOnlyList<string> selected, int? max)
    {
        Max = max;
        _all = [.. all.Select(c => new CuisineOption(c) { IsChecked = selected.Contains(c.Code) })];
        Visible = _all;
    }

    public int? Max { get; }
    public bool ShowCounter => Max is not null;
    public string CounterText => $"{Format.Number(_all.Count(o => o.IsChecked))} / {Format.Number(Max ?? 0)}";

    [ObservableProperty] public partial IReadOnlyList<CuisineOption> Visible { get; set; }
    [ObservableProperty] public partial string Search { get; set; } = "";
    [ObservableProperty] public partial string? Message { get; set; }

    public IReadOnlyList<string> SelectedCodes => [.. _all.Where(o => o.IsChecked).Select(o => o.Cuisine.Code)];

    partial void OnSearchChanged(string value) =>
        Visible = string.IsNullOrWhiteSpace(value) ? _all
            : [.. _all.Where(o => o.Cuisine.Name.Contains(value.Trim(), StringComparison.OrdinalIgnoreCase)
                               || o.Cuisine.NameEn.Contains(value.Trim(), StringComparison.OrdinalIgnoreCase))];

    [RelayCommand]
    void Toggle(CuisineOption option)
    {
        Message = null;
        if (!option.IsChecked && Max is { } max && _all.Count(o => o.IsChecked) >= max)
        {
            Message = string.Format(AppResources.MaxCuisines, Format.Number(max)); // a 4th pick is blocked
            return;
        }
        option.IsChecked = !option.IsChecked;
        OnPropertyChanged(nameof(CounterText));
    }

    [RelayCommand]
    void Clear()
    {
        foreach (var o in _all) o.IsChecked = false;
        Message = null;
        OnPropertyChanged(nameof(CounterText));
    }
}
