using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FridgeChef.Core.Auth;
using FridgeChef.Core.Recipes;
using FridgeChef.Core.Resources;

namespace FridgeChef.Core.ViewModels;

public sealed partial class CuisineChip(Cuisine cuisine) : ObservableObject
{
    public Cuisine Cuisine => cuisine;
    [ObservableProperty] public partial bool IsSelected { get; set; }
}

// Figma 6.1 Fridge page: up to 3 cuisines, ingredients typed or tapped, then Find recipes.
public sealed partial class FridgeViewModel(RecipeClient client, LocalCache cache, AuthService auth, IAppNavigator nav, IToast toast) : ObservableObject
{
    static readonly string[] QuickCuisines = ["KH", "VN", "CN", "JP", "KR"]; // the chips shown in Figma; others via "More countries"
    List<Cuisine> _all = [];
    bool _started;
    string Key => "fridge." + auth.UserId;

    public ObservableCollection<CuisineChip> CuisineChips { get; } = [];
    public ObservableCollection<string> Ingredients { get; } = [];
    public IReadOnlyList<string> QuickItems { get; } =
        [AppResources.QuickOnion, AppResources.QuickGarlic, AppResources.QuickFish, AppResources.QuickLime, AppResources.QuickPork, AppResources.QuickNoodles];

    [ObservableProperty] public partial string NewIngredient { get; set; } = "";
    [ObservableProperty] public partial string? Message { get; set; }

    public string CounterText => $"{Format.Number(Selected.Count())} / {Format.Number(FridgeRules.MaxCuisines)}";
    public bool HasIngredients => Ingredients.Count > 0;
    IEnumerable<Cuisine> Selected => CuisineChips.Where(c => c.IsSelected).Select(c => c.Cuisine);

    public async Task AppearingAsync()
    {
        if (_started) return;
        _started = true;
        var saved = await cache.GetAsync(Key, SuggestJson.Default.FridgeState) ?? new FridgeState();
        foreach (var i in saved.Ingredients) Ingredients.Add(i);
        try { _all = await client.GetCuisinesAsync(); await cache.PutAsync("cuisines", _all, RecipeJson.Default.ListCuisine); }
        catch (AuthException) { _all = await cache.GetAsync("cuisines", RecipeJson.Default.ListCuisine) ?? []; }
        ShowChips(saved.Cuisines);
        Refresh();
    }

    void ShowChips(IReadOnlyCollection<string> selected)
    {
        CuisineChips.Clear();
        // Quick chips in Figma order, plus any other chosen country (Cambodia stays first: list is sorted).
        foreach (var c in _all.Where(c => QuickCuisines.Contains(c.Code) || selected.Contains(c.Code)))
            CuisineChips.Add(new CuisineChip(c) { IsSelected = selected.Contains(c.Code) });
    }

    [RelayCommand]
    void ToggleCuisine(CuisineChip chip)
    {
        Message = null;
        if (!chip.IsSelected && Selected.Count() >= FridgeRules.MaxCuisines)
        {
            toast.Show(ToastKind.Info, string.Format(AppResources.MaxCuisines, Format.Number(FridgeRules.MaxCuisines))); // 4th pick blocked
            return;
        }
        chip.IsSelected = !chip.IsSelected;
        Refresh();
    }

    [RelayCommand]
    async Task MoreCountries()
    {
        if (_all.Count == 0) { toast.Show(ToastKind.Offline, AppResources.NoInternetTitle, AppResources.NoInternetSub); return; }
        var picked = await nav.PickCuisinesAsync(_all, [.. Selected.Select(c => c.Code)], FridgeRules.MaxCuisines);
        if (picked is null) return;
        ShowChips(picked);
        Refresh();
    }

    [RelayCommand] void AddIngredient() { if (Add(NewIngredient)) NewIngredient = ""; }
    [RelayCommand] void AddQuick(string item) => Add(item);

    bool Add(string? text)
    {
        Message = null;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (FridgeRules.Clean(text) is not { } item) { Message = AppResources.BadIngredient; return false; }
        if (Ingredients.Contains(item)) return true; // already there
        if (Ingredients.Count >= FridgeRules.MaxItems) { Message = AppResources.MaxIngredients; return false; }
        Ingredients.Add(item);
        Refresh();
        return true;
    }

    [RelayCommand]
    void RemoveIngredient(string item)
    {
        Ingredients.Remove(item);
        Refresh();
    }

    [RelayCommand]
    Task FindRecipes()
    {
        if (Ingredients.Count < FridgeRules.MinItems) { Message = AppResources.NeedTwo; return Task.CompletedTask; }
        var cuisines = Selected.ToList();
        var request = new SuggestRequest
        {
            Ingredients = [.. Ingredients],
            Cuisines = [.. cuisines.Select(c => c.Code)],
            Language = Format.IsKhmer ? "km" : "en",
        };
        var subtitle = string.Format(AppResources.FromIngredients, string.Join(", ", Ingredients)) +
                       (cuisines.Count > 0 ? " · " + string.Join(", ", cuisines.Select(c => c.Adjective)) : "");
        return nav.OpenSuggestionsAsync(request, subtitle);
    }

    [RelayCommand] Task Back() => nav.BackAsync();

    // Update counters and keep the fridge on the phone for next time.
    void Refresh()
    {
        OnPropertyChanged(nameof(CounterText));
        OnPropertyChanged(nameof(HasIngredients));
        _ = cache.PutAsync(Key, new FridgeState { Ingredients = [.. Ingredients], Cuisines = [.. Selected.Select(c => c.Code)] }, SuggestJson.Default.FridgeState);
    }
}

// Figma 6.3 (loading), 6.2 (results), 14 (busy), 14.1 (daily limit): one page, four states.
public sealed partial class SuggestionsViewModel(SuggestClient client, IAppNavigator nav, IToast toast) : ObservableObject
{
    public const int DailyLimit = 10;
    SuggestRequest? _request;
    CancellationTokenSource? _cts;

    [ObservableProperty] public partial string Subtitle { get; set; } = "";
    [ObservableProperty] public partial IReadOnlyList<Suggestion> Results { get; set; } = [];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading), nameof(ShowResults), nameof(IsBusy), nameof(IsLimit))]
    public partial string State { get; set; } = "loading";

    public bool IsLoading => State == "loading";
    public bool ShowResults => State == "results";
    public bool IsBusy => State == "busy";
    public bool IsLimit => State == "limit";
    public string LimitTitle => string.Format(AppResources.LimitTitle, Format.Number(DailyLimit));

    public Task StartAsync(SuggestRequest request, string subtitle)
    {
        _request = request;
        Subtitle = subtitle;
        return RunAsync();
    }

    async Task RunAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        State = "loading";
        try
        {
            Results = await client.SuggestAsync(_request!, _cts.Token);
            State = "results";
        }
        catch (OperationCanceledException) { } // Cancel pressed: the page is already closing
        catch (AuthException e) when (e.Code == SuggestClient.RateLimit) { State = "limit"; }
        catch (AuthException e) when (e.Code == AuthException.Network)
        {
            toast.Show(ToastKind.Offline, AppResources.NoInternetTitle, AppResources.NoInternetSub);
            await nav.BackAsync(); // back to the fridge, nothing lost
        }
        catch (AuthException) { State = "busy"; } // AI_BUSY or anything unexpected: friendly "chef is busy"
    }

    [RelayCommand] Task TryAgain() => RunAsync();

    [RelayCommand]
    Task Cancel()
    {
        _cts?.Cancel();
        return nav.BackAsync();
    }

    [RelayCommand] Task Open(Suggestion s) => nav.OpenSuggestionAsync(s);
    [RelayCommand] Task GoTo(string route) => nav.OpenTabAsync(route);
}

// Figma 13: one suggested recipe with what you have / what is missing.
public sealed partial class SuggestionDetailViewModel(SavedRecipes saved, IAppNavigator nav, IToast toast) : ObservableObject
{
    [ObservableProperty] public partial Suggestion? Suggestion { get; set; }
    [ObservableProperty] public partial bool IsSaved { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    public string SaveText => IsSaved ? AppResources.SavedButton : AppResources.SaveRecipe;

    public void Show(Suggestion s)
    {
        Suggestion = s;
        Update();
    }

    void Update()
    {
        IsSaved = Suggestion is not null && saved.FindSuggestion(Suggestion) is not null;
        OnPropertyChanged(nameof(SaveText));
    }

    [RelayCommand]
    async Task ToggleSave()
    {
        if (Suggestion is not { } s || IsBusy) return;
        IsBusy = true;
        try
        {
            if (saved.FindSuggestion(s) is { } item)
            {
                await saved.RemoveItemAsync(item);
                toast.Show(ToastKind.Removed, AppResources.RemovedToast);
            }
            else
            {
                var added = await saved.SaveSuggestionAsync(s);
                toast.Show(ToastKind.Success, AppResources.RecipeSavedToast, added.SavedOnText);
            }
        }
        catch (AuthException e) { Errors.Show(toast, e); }
        finally { IsBusy = false; Update(); }
    }

    [RelayCommand]
    Task Share()
    {
        if (Suggestion is not { } s) return Task.CompletedTask;
        var text = new StringBuilder().AppendLine(s.Name).AppendLine(s.CaloriesEstimateText).AppendLine()
            .AppendLine(s.Description).AppendLine().AppendLine(AppResources.Ingredients);
        foreach (var i in s.Ingredients) text.AppendLine("• " + i.Text);
        text.AppendLine().AppendLine(AppResources.Steps);
        foreach (var st in s.StepList) text.AppendLine($"{st.Number}. {st.Text}");
        return nav.ShareTextAsync(s.Name, text.ToString());
    }

    [RelayCommand] Task Back() => nav.BackAsync();
}
