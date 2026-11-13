using System.Text.Json.Serialization.Metadata;
using System.Diagnostics.CodeAnalysis;
using SQLite;

namespace FridgeChef.Core.Recipes;

// Phone-side copy of what the app last showed (SQLite file), so it opens fast and saved recipes work offline.
// Each list is stored as one JSON blob under a key: "home.trending", "saved.<user id>", ...
public sealed class LocalCache(string path)
{
    sealed class Entry
    {
        [PrimaryKey] public string Key { get; set; } = "";
        public string Json { get; set; } = "";
        public long UpdatedAt { get; set; }
    }

    SQLiteAsyncConnection? _db;

    // sqlite-net finds Entry's constructor and properties by reflection; keep them in the trimmed release build.
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, typeof(Entry))]
    async Task<SQLiteAsyncConnection> Db()
    {
        if (_db is null)
        {
            var db = new SQLiteAsyncConnection(path);
            await db.CreateTableAsync<Entry>();
            _db = db;
        }
        return _db;
    }

    public async Task<T?> GetAsync<T>(string key, JsonTypeInfo<T> type)
    {
        var row = await (await Db()).FindAsync<Entry>(key);
        if (row is null) return default;
        try { return System.Text.Json.JsonSerializer.Deserialize(row.Json, type); }
        catch (System.Text.Json.JsonException) { return default; } // old format after an update: just refetch
    }

    public async Task PutAsync<T>(string key, T value, JsonTypeInfo<T> type) =>
        await (await Db()).InsertOrReplaceAsync(new Entry
        {
            Key = key,
            Json = System.Text.Json.JsonSerializer.Serialize(value, type),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        });

    // Settings → Clear cache: drop the given lists (they are fetched again when needed).
    public async Task RemoveAsync(params string[] keys)
    {
        var db = await Db();
        foreach (var k in keys) await db.DeleteAsync<Entry>(k);
    }

    // Log out: forget everything of the previous user (saved list, fridge list).
    public async Task ClearAsync() => await (await Db()).DeleteAllAsync<Entry>();
}
