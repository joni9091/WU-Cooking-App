using FridgeChef.Core;

namespace FridgeChef.Tests;

public class ValidationTests
{
    [Theory]
    [InlineData("joni.cooks", true)]
    [InlineData("@Joni_99", true)]      // leading @ and capitals are normalised
    [InlineData("ab", false)]           // too short
    [InlineData("abcdefghijklmnopqrstu", false)] // 21 chars
    [InlineData("has space", false)]
    [InlineData("khmer_ខ្មែរ", false)]
    [InlineData("", false)]
    public void Handle(string input, bool ok) => Assert.Equal(ok, Validation.IsHandle(input));

    [Fact]
    public void Handle_is_stored_lower_case_without_at() => Assert.Equal("joni.cooks", Validation.NormalizeHandle(" @Joni.Cooks "));

    [Theory]
    [InlineData("you@email.com", true)]
    [InlineData("  you@email.com ", true)]
    [InlineData("you@email", false)]
    [InlineData("you email.com", false)]
    [InlineData(null, false)]
    public void Email(string? input, bool ok) => Assert.Equal(ok, Validation.IsEmail(input));

    [Theory]
    [InlineData("12345678", true)]
    [InlineData("1234567", false)]
    [InlineData(null, false)]
    public void Password(string? input, bool ok) => Assert.Equal(ok, Validation.IsPassword(input));

    [Theory]
    [InlineData("Joni", true)]
    [InlineData("   ", false)]
    [InlineData("ចន្ទ", true)]
    public void DisplayName(string input, bool ok) => Assert.Equal(ok, Validation.IsDisplayName(input));
}
