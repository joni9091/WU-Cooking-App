using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FridgeChef.Core.Auth;
using FridgeChef.Core.Resources;

namespace FridgeChef.Core.Recipes;

// One recipe idea from the suggest-recipes function. Written in the language it was asked in.
public sealed class Suggestion
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("calories_per_serving")] public int Calories { get; set; }
    [JsonPropertyName("cook_time_min")] public int CookTimeMin { get; set; }
    [JsonPropertyName("servings")] public int Servings { get; set; }
    [JsonPropertyName("have")] public string[] Have { get; set; } = [];
    [JsonPropertyName("missing")] public string[] Missing { get; set; } = [];
    [JsonPropertyName("steps")] public string[] Steps { get; set; } = [];

    // Estimated by the server, so always shown as approximate (brief): "About 420 kcal / serving".
    [JsonIgnore] public string CaloriesText => string.Format(AppResources.AboutKcal, Format.Number(Calories));
    [JsonIgnore] public string CaloriesEstimateText => string.Format(AppResources.AboutKcalEstimate, Format.Number(Calories));
    [JsonIgnore] public int TotalIngredients => Have.Length + Missing.Length;
    [JsonIgnore] public string HaveText => string.Format(AppResources.YouHaveXofY, Format.Number(Have.Length), Format.Number(TotalIngredients));
    [JsonIgnore] public bool HasMissing => Missing.Length > 0;
    [JsonIgnore] public string MissingText => string.Format(AppResources.MissingList, string.Join(", ", Missing));
    [JsonIgnore] public string SummaryText => HasMissing
        ? string.Format(AppResources.HaveSummaryMissing, Format.Number(Have.Length), Format.Number(TotalIngredients), string.Join(", ", Missing))
        : string.Format(AppResources.HaveSummary, Format.Number(Have.Length), Format.Number(TotalIngredients));
    [JsonIgnore] public string TimeText => string.Format(AppResources.Minutes, Format.Number(CookTimeMin));
    [JsonIgnore] public string ServingsText => string.Format(AppResources.Servings, Format.Number(Servings));
    [JsonIgnore] public IReadOnlyList<IngredientLine> Ingredients =>
        [.. Have.Select(h => new IngredientLine(h, true)), .. Missing.Select(m => new IngredientLine(m, false))];
    [JsonIgnore] public IReadOnlyList<Step> StepList => [.. Steps.Select((s, i) => new Step(Format.Number(i + 1), s))];
}

public sealed record IngredientLine(string Text, bool Have);

// What the user sends: the same rules are checked again on the server.
public static partial class FridgeRules
{
    public const int MinItems = 2, MaxItems = 20, MaxItemLength = 40, MaxCuisines = 3;

    // Letters of any language (incl. Khmer vowel marks), digits, spaces and basic punctuation.
    [GeneratedRegex(@"^[\p{L}\p{M}\p{N} .,'()&\-/]+$")]
    private static partial Regex Allowed();

    // Cleans one typed ingredient; null if it can't be used.
    public static string? Clean(string? text)
    {
        var s = Regex.Replace((text ?? "").Trim(), @"\s+", " ").ToLowerInvariant();
        return s.Length is >= 1 and <= MaxItemLength && Allowed().IsMatch(s) ? s : null;
    }
}

public sealed class SuggestRequest
{
    [JsonPropertyName("ingredients")] public string[] Ingredients { get; set; } = [];
    [JsonPropertyName("cuisines")] public string[] Cuisines { get; set; } = [];
    [JsonPropertyName("language")] public string Language { get; set; } = "en";
}

sealed class SuggestResponse
{
    [JsonPropertyName("recipes")] public List<Suggestion>? Recipes { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
}

// Calls the suggest-recipes function. Errors: AuthException("RATE_LIMIT_USER" | "AI_BUSY" | "network" | ...).
public sealed class SuggestClient(Api api)
{
    public const string RateLimit = "RATE_LIMIT_USER", Busy = "AI_BUSY";

    public async Task<List<Suggestion>> SuggestAsync(SuggestRequest request, CancellationToken cancel)
    {
        using var res = await api.SendAsync(HttpMethod.Post, "functions/v1/suggest-recipes",
            JsonContent.Create(request, SuggestJson.Default.SuggestRequest), null, cancel,
            HttpStatusCode.TooManyRequests, HttpStatusCode.ServiceUnavailable, HttpStatusCode.BadRequest);
        var body = await res.Content.ReadFromJsonAsync(SuggestJson.Default.SuggestResponse, cancel);
        if (res.IsSuccessStatusCode && body?.Recipes is { Count: > 0 } list) return list;
        throw new AuthException(body?.Error ?? $"http_{(int)res.StatusCode}");
    }
}

// What is in the fridge: kept on the phone per user (brief: SQLite), so it is still there next time.
public sealed class FridgeState
{
    [JsonPropertyName("ingredients")] public List<string> Ingredients { get; set; } = [];
    [JsonPropertyName("cuisines")] public List<string> Cuisines { get; set; } = [];
}

[JsonSerializable(typeof(SuggestRequest))]
[JsonSerializable(typeof(SuggestResponse))]
[JsonSerializable(typeof(List<Suggestion>))]
[JsonSerializable(typeof(FridgeState))]
partial class SuggestJson : JsonSerializerContext;
