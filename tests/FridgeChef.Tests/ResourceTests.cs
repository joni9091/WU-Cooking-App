using System.Collections;
using System.Globalization;
using FridgeChef.Core.Resources;

namespace FridgeChef.Tests;

public class ResourceTests
{
    // Every English text must have a non-empty Khmer translation, so no screen shows a missing string.
    [Fact]
    public void Khmer_has_every_English_key()
    {
        var en = Keys(CultureInfo.InvariantCulture);
        var km = Keys(new CultureInfo("km"));
        Assert.NotEmpty(en);
        Assert.Empty(en.Except(km));
    }

    static HashSet<string> Keys(CultureInfo culture) =>
        AppResources.ResourceManager.GetResourceSet(culture, true, false)!
            .Cast<DictionaryEntry>()
            .Where(e => !string.IsNullOrWhiteSpace(e.Value as string))
            .Select(e => (string)e.Key)
            .ToHashSet();
}
