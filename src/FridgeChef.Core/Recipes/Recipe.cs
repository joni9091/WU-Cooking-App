using System.Text.Json.Serialization;
using FridgeChef.Core.Resources;

namespace FridgeChef.Core.Recipes;

// One row of the recipes table. Display properties pick the app language and fall back to English when Khmer is missing.
public sealed class Recipe
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("title_en")] public string TitleEn { get; set; } = "";
    [JsonPropertyName("title_km")] public string? TitleKm { get; set; }
    [JsonPropertyName("description_en")] public string DescriptionEn { get; set; } = "";
    [JsonPropertyName("description_km")] public string? DescriptionKm { get; set; }
    [JsonPropertyName("calories")] public int Calories { get; set; }
    [JsonPropertyName("cuisine")] public string Cuisine { get; set; } = "";
    [JsonPropertyName("category")] public string Category { get; set; } = "";
    [JsonPropertyName("prep_minutes")] public int PrepMinutes { get; set; }
    [JsonPropertyName("servings")] public int Servings { get; set; }
    [JsonPropertyName("difficulty")] public string Difficulty { get; set; } = "easy";
    [JsonPropertyName("image_url")] public string? ImageUrl { get; set; }
    [JsonPropertyName("image_credit")] public string? ImageCredit { get; set; }
    [JsonPropertyName("ingredients_en")] public string[] IngredientsEn { get; set; } = [];
    [JsonPropertyName("ingredients_km")] public string[] IngredientsKm { get; set; } = [];
    [JsonPropertyName("steps_en")] public string[] StepsEn { get; set; } = [];
    [JsonPropertyName("steps_km")] public string[] StepsKm { get; set; } = [];
    [JsonPropertyName("is_trending")] public bool IsTrending { get; set; }
    [JsonPropertyName("popularity")] public int Popularity { get; set; }

    public const string Columns = "id,title_en,title_km,description_en,description_km,calories,cuisine,category,prep_minutes,servings," +
                                  "difficulty,image_url,image_credit,ingredients_en,ingredients_km,steps_en,steps_km,is_trending,popularity";

    static string Pick(string? km, string en) => Format.IsKhmer && !string.IsNullOrWhiteSpace(km) ? km : en;
    static string[] Pick(string[] km, string[] en) => Format.IsKhmer && km.Length > 0 ? km : en;

    [JsonIgnore] public string Title => Pick(TitleKm, TitleEn);
    [JsonIgnore] public string Description => Pick(DescriptionKm, DescriptionEn);
    [JsonIgnore] public string[] Ingredients => Pick(IngredientsKm, IngredientsEn);
    [JsonIgnore] public IReadOnlyList<Step> Steps => [.. Pick(StepsKm, StepsEn).Select((s, i) => new Step(Format.Number(i + 1), s))];
    [JsonIgnore] public string CaloriesText => Format.Kcal(Calories);
    [JsonIgnore] public string TimeText => string.Format(AppResources.Minutes, Format.Number(PrepMinutes));
    [JsonIgnore] public string ServingsText => string.Format(AppResources.Servings, Format.Number(Servings));
    [JsonIgnore] public string DifficultyText => Difficulty switch
    {
        "hard" => AppResources.DifficultyHard,
        "medium" => AppResources.DifficultyMedium,
        _ => AppResources.DifficultyEasy,
    };
    [JsonIgnore] public bool HasImage => !string.IsNullOrEmpty(ImageUrl);
}

public sealed record Step(string Number, string Text);

public sealed class Cuisine
{
    [JsonPropertyName("code")] public string Code { get; set; } = "";
    [JsonPropertyName("name_en")] public string NameEn { get; set; } = "";
    [JsonPropertyName("name_km")] public string NameKm { get; set; } = "";
    [JsonPropertyName("adjective_en")] public string? AdjectiveEn { get; set; }
    [JsonPropertyName("adjective_km")] public string? AdjectiveKm { get; set; }
    [JsonPropertyName("flag_emoji")] public string FlagEmoji { get; set; } = "";

    [JsonIgnore] public string Name => Format.IsKhmer ? NameKm : NameEn;                               // "Cambodia" (Recipe filter)
    [JsonIgnore] public string Adjective => Format.IsKhmer ? AdjectiveKm ?? NameKm : AdjectiveEn ?? NameEn; // "Cambodian" (Fridge page)
}

public sealed class SavedItem
{
    [JsonPropertyName("recipe_id")] public string RecipeId { get; set; } = "";
    [JsonPropertyName("created_at")] public DateTimeOffset SavedAt { get; set; }
    [JsonPropertyName("recipes")] public Recipe? Recipe { get; set; }

    [JsonIgnore] public string SavedOnText => string.Format(AppResources.SavedOn, Format.Date(SavedAt));
}
