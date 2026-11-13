namespace FridgeChef.Core;

// Supabase project the app talks to.
// The publishable key is meant to ship inside apps: Row Level Security protects the data.
// NEVER put the secret / service_role key here or anywhere in the app.
public static class SupabaseConfig
{
    public const string Url = "https://vobwgptmuzichrcvutwq.supabase.co";
    public const string PublishableKey = "sb_publishable_DXzqXtvM8jrZEKEErEFl1Q_XvupVU1x";
}

// Shown in About → Contact us and the privacy policy (owner approved it being public).
public static class AppContact
{
    public const string Email = "sanlong73@gmail.com";
}
