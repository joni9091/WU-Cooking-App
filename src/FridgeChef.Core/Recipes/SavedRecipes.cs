using FridgeChef.Core.Auth;

namespace FridgeChef.Core.Recipes;

// The signed-in user's saved recipes, shared by the Saved tab and the recipe detail page.
// Shows the phone copy first, then the server copy; changes go to the server first and are undone on failure.
public sealed class SavedRecipes(RecipeClient client, LocalCache cache, AuthService auth)
{
    List<SavedItem> _items = [];
    string Key => "saved." + auth.UserId;

    public IReadOnlyList<SavedItem> Items => _items;
    public event Action? Changed;

    public bool IsSaved(string recipeId) => _items.Any(i => i.RecipeId == recipeId);
    public SavedItem? Find(string recipeId) => _items.FirstOrDefault(i => i.RecipeId == recipeId);

    // Phone copy (instant, works offline).
    public async Task LoadCachedAsync()
    {
        if (await cache.GetAsync(Key, RecipeJson.Default.ListSavedItem) is { } cached) Set(cached);
    }

    // Server copy. Throws AuthException("network") when offline; the phone copy stays on screen.
    public async Task RefreshAsync()
    {
        var fresh = await client.GetSavedAsync();
        Set(fresh);
        await cache.PutAsync(Key, fresh, RecipeJson.Default.ListSavedItem);
    }

    public async Task SaveAsync(Recipe recipe)
    {
        if (IsSaved(recipe.Id)) return;
        await client.SaveAsync(recipe.Id); // throws when offline: nothing changes on screen
        Set([new SavedItem { RecipeId = recipe.Id, SavedAt = DateTimeOffset.UtcNow, Recipe = recipe }, .. _items]);
        await cache.PutAsync(Key, _items, RecipeJson.Default.ListSavedItem);
    }

    public async Task RemoveAsync(string recipeId)
    {
        await client.UnsaveAsync(recipeId);
        Set([.. _items.Where(i => i.RecipeId != recipeId)]);
        await cache.PutAsync(Key, _items, RecipeJson.Default.ListSavedItem);
    }

    // Log out: forget the previous user's list in memory.
    public void Clear() => Set([]);

    void Set(List<SavedItem> items)
    {
        _items = [.. items.Where(i => i.Recipe is not null).OrderByDescending(i => i.SavedAt)];
        Changed?.Invoke();
    }
}
