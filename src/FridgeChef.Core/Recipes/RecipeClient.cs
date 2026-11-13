using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FridgeChef.Core.Auth;

namespace FridgeChef.Core.Recipes;

// What to load. Popular first (Cambodian dishes carry the highest popularity), id breaks ties so pages never overlap.
public sealed record RecipeQuery(string? Search = null, string? Category = null, IReadOnlyList<string>? Cuisines = null,
                                 bool TrendingOnly = false, int Offset = 0, int Limit = RecipeQuery.PageSize)
{
    public const int PageSize = 20;
    public const int MaxSearchLength = 40;

    // Keeps letters (any script, incl. Khmer marks), digits and spaces: everything else could break the filter syntax.
    public static string CleanSearch(string? text)
    {
        var kept = string.Concat((text ?? "").Where(c => char.IsLetterOrDigit(c) || c == ' ' ||
            CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark)).Trim();
        return kept.Length > MaxSearchLength ? kept[..MaxSearchLength].Trim() : kept;
    }

    public string ToPath()
    {
        var q = $"rest/v1/recipes?select={Recipe.Columns}&order=popularity.desc,id.asc&limit={Limit}&offset={Offset}";
        if (TrendingOnly) q += "&is_trending=eq.true";
        if (!string.IsNullOrEmpty(Category)) q += $"&category=eq.{Uri.EscapeDataString(Category)}";
        if (Cuisines is { Count: > 0 }) q += $"&cuisine=in.({string.Join(',', Cuisines.Select(Uri.EscapeDataString))})";
        var s = CleanSearch(Search);
        if (s.Length > 0)
        {
            var like = Uri.EscapeDataString($"*{s}*");
            q += $"&or=(title_en.ilike.{like},title_km.ilike.{like})";
        }
        return q;
    }
}

// Reads recipes and countries (read-only for app users; edited only in the Supabase dashboard).
public sealed class RecipeClient(Api api)
{
    public async Task<List<Recipe>> GetAsync(RecipeQuery query)
    {
        using var res = await api.SendAsync(HttpMethod.Get, query.ToPath());
        return await res.Content.ReadFromJsonAsync(RecipeJson.Default.ListRecipe) ?? [];
    }

    public async Task<List<Cuisine>> GetCuisinesAsync()
    {
        using var res = await api.SendAsync(HttpMethod.Get, "rest/v1/cuisines?select=code,name_en,name_km,adjective_en,adjective_km,flag_emoji&order=sort_order");
        return await res.Content.ReadFromJsonAsync(RecipeJson.Default.ListCuisine) ?? [];
    }

    // The signed-in user's saved recipes, newest first, each with its full recipe (so the list also works offline).
    public async Task<List<SavedItem>> GetSavedAsync()
    {
        using var res = await api.SendAsync(HttpMethod.Get,
            $"rest/v1/saved_recipes?select=id,recipe_id,created_at,ai_recipe,recipes({Recipe.Columns})&order=created_at.desc");
        return await res.Content.ReadFromJsonAsync(RecipeJson.Default.ListSavedItem) ?? [];
    }

    // Saving twice is fine: the database keeps one row per user and recipe (409 = already saved).
    public async Task SaveAsync(string recipeId)
    {
        var row = new SaveRow { UserId = api.Auth.UserId, RecipeId = recipeId };
        using var _ = await api.SendAsync(HttpMethod.Post, "rest/v1/saved_recipes", JsonContent.Create(row, RecipeJson.Default.SaveRow),
            r => r.Headers.Add("Prefer", "return=minimal"), HttpStatusCode.Conflict);
    }

    // Saves a suggestion (it has no recipe row) and returns the new saved row.
    public async Task<SavedItem> SaveSuggestionAsync(Suggestion suggestion)
    {
        var row = new SaveRow { UserId = api.Auth.UserId, AiRecipe = suggestion };
        using var res = await api.SendAsync(HttpMethod.Post, "rest/v1/saved_recipes?select=id,recipe_id,created_at,ai_recipe",
            JsonContent.Create(row, RecipeJson.Default.SaveRow), r => r.Headers.Add("Prefer", "return=representation"));
        var rows = await res.Content.ReadFromJsonAsync(RecipeJson.Default.ListSavedItem);
        return rows is [var saved, ..] ? saved : throw new AuthException("bad_response");
    }

    public async Task DeleteSavedRowAsync(string rowId)
    {
        using var _ = await api.SendAsync(HttpMethod.Delete, $"rest/v1/saved_recipes?id=eq.{Uri.EscapeDataString(rowId)}");
    }

    public async Task UnsaveAsync(string recipeId)
    {
        using var _ = await api.SendAsync(HttpMethod.Delete, $"rest/v1/saved_recipes?recipe_id=eq.{Uri.EscapeDataString(recipeId)}");
    }
}

sealed class SaveRow
{
    [JsonPropertyName("user_id")] public string? UserId { get; set; }
    [JsonPropertyName("recipe_id")] public string? RecipeId { get; set; }
    [JsonPropertyName("ai_recipe")] public Suggestion? AiRecipe { get; set; }
}

[JsonSerializable(typeof(List<Recipe>))]
[JsonSerializable(typeof(List<Cuisine>))]
[JsonSerializable(typeof(List<SavedItem>))]
[JsonSerializable(typeof(SaveRow))]
partial class RecipeJson : JsonSerializerContext;
