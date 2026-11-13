using System.Text.RegularExpressions;

namespace FridgeChef.Core;

// Input rules shared by every screen. They mirror the database checks so the app catches mistakes first.
public static partial class Validation
{
    public const int MinPasswordLength = 8;

    // Same pattern as the profiles.handle check in the database (dots allowed, owner decision).
    [GeneratedRegex("^[a-z0-9._]{3,20}$")]
    private static partial Regex HandleRegex();

    // Simple shape check only; the server is the real judge of whether an email works.
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();

    public static bool IsEmail(string? s) => s is not null && s.Length <= 254 && EmailRegex().IsMatch(s.Trim());

    public static bool IsPassword(string? s) => s is not null && s.Length >= MinPasswordLength;

    // Handles are stored lower-case without the leading @ shown in the UI.
    public static string NormalizeHandle(string? s) => (s ?? "").Trim().TrimStart('@').ToLowerInvariant();

    public static bool IsHandle(string? s) => HandleRegex().IsMatch(NormalizeHandle(s));

    public static bool IsDisplayName(string? s) => s?.Trim().Length is >= 1 and <= 40;
}
