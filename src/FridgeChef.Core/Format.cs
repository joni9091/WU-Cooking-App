using System.Globalization;
using FridgeChef.Core.Resources;

namespace FridgeChef.Core;

// Numbers and dates in the app language. Khmer mode uses Khmer numerals (owner decision): 420 -> ៤២០.
public static class Format
{
    public static bool IsKhmer => (AppResources.Culture ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName == "km";

    public static string Number(long n) => Digits(n.ToString(CultureInfo.InvariantCulture));

    public static string Digits(string s) =>
        IsKhmer ? string.Concat(s.Select(c => c is >= '0' and <= '9' ? (char)('០' + (c - '0')) : c)) : s;

    // Always "2 Dec 2026" (day, short month name, year): never 2/12/2026, which people read differently.
    // Stored in UTC, shown in the phone's local time. Month names come from the resx so they never depend on the phone's culture data.
    public static string Date(DateTimeOffset when, TimeZoneInfo? zone = null)
    {
        var local = TimeZoneInfo.ConvertTime(when, zone ?? TimeZoneInfo.Local);
        var month = AppResources.ResourceManager.GetString($"Month{local.Month}", AppResources.Culture) ?? local.Month.ToString(CultureInfo.InvariantCulture);
        return $"{Number(local.Day)} {month} {Number(local.Year)}";
    }

    public static string Kcal(int calories) => string.Format(AppResources.KcalPerServing, Number(calories));
}
