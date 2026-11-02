using Babel14.Services;
using Xunit;

namespace Babel14.Tests;

public class LanguagesTests
{
    [Fact]
    public void JapaneseAndEnglishLeadTheList()
    {
        Assert.Equal(new[]
        {
            "Japanese", "English", "French", "German", "Spanish", "Malay", "Indonesian", "Korean",
        }, Languages.All);
    }

    [Fact]
    public void DefaultsAreJapaneseIncomingAndEnglishKnown()
    {
        Assert.Equal("Japanese", Languages.DefaultNative);
        Assert.Equal("English", Languages.DefaultKnown);
        Assert.Equal(Languages.All[0], Languages.DefaultNative);
        Assert.Equal(Languages.All[1], Languages.DefaultKnown);
    }

    [Theory]
    [InlineData("Korean", 7)]
    [InlineData("japanese", 0)] // config values are exact; an unknown spelling falls back rather than throwing
    [InlineData(null, 0)]
    [InlineData("", 0)]
    public void IndexOfFallsBackToTheFirstEntry(string? language, int expected) =>
        Assert.Equal(expected, Languages.IndexOf(language));
}
