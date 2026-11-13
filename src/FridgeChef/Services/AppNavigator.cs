using FridgeChef.Core.Recipes;
using FridgeChef.Core.ViewModels;
using FridgeChef.Views;

namespace FridgeChef.Services;

// Navigation, the country sheet and sharing for the recipe screens.
public sealed class AppNavigator : IAppNavigator, INewsNavigator
{
    public const string DetailRoute = "recipe-detail", FridgeRoute = "fridge", SuggestionsRoute = "suggestions", SuggestionRoute = "suggestion", ArticleRoute = "news-article";

    public Task OpenArticleAsync(NewsItem item) =>
        Shell.Current.GoToAsync(ArticleRoute, new ShellNavigationQueryParameters { ["item"] = item });

    // The phone's browser (Chrome custom tab), so the user stays signed in to the news site and can come back.
    public Task OpenInBrowserAsync(Uri url) => Browser.Default.OpenAsync(url, BrowserLaunchMode.SystemPreferred);

    public Task OpenRecipeAsync(Recipe recipe) =>
        Shell.Current.GoToAsync(DetailRoute, new ShellNavigationQueryParameters { ["recipe"] = recipe });

    public Task OpenFridgeAsync() => Shell.Current.GoToAsync(FridgeRoute);

    public Task OpenSuggestionsAsync(SuggestRequest request, string subtitle) =>
        Shell.Current.GoToAsync(SuggestionsRoute, new ShellNavigationQueryParameters { ["request"] = request, ["subtitle"] = subtitle });

    public Task OpenSuggestionAsync(Suggestion suggestion) =>
        Shell.Current.GoToAsync(SuggestionRoute, new ShellNavigationQueryParameters { ["suggestion"] = suggestion });

    public Task OpenTabAsync(string route) => Shell.Current.GoToAsync("//" + route);

    public Task BackAsync() => Shell.Current.GoToAsync("..");

    public async Task<IReadOnlyList<string>?> PickCuisinesAsync(IReadOnlyList<Cuisine> all, IReadOnlyList<string> selected, int? max)
    {
        var sheet = new CuisinePickerPage(new CuisinePickerViewModel(all, selected, max));
        await Shell.Current.Navigation.PushModalAsync(sheet, animated: false);
        return await sheet.Result;
    }

    public Task ShareTextAsync(string title, string text) =>
        Share.Default.RequestAsync(new ShareTextRequest { Title = title, Text = text });
}

// Shows a toast on the screen the user is looking at.
public sealed class ToastService : IToast
{
    public void Show(ToastKind kind, string title, string? detail = null, string? action = null, Func<Task>? onAction = null) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var page = Shell.Current?.Navigation.ModalStack.LastOrDefault() ?? Shell.Current?.CurrentPage;
            if ((page as ContentPage)?.Content is Layout root && root.Children.OfType<ToastView>().FirstOrDefault() is { } toast)
                toast.Show(kind, title, detail, action, onAction);
        });
}
