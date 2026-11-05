using Babel14.Services;
using Xunit;

namespace Babel14.Tests;

/// <summary>
/// A model asked to translate a message that is already in the target language tends to hand it
/// back with the quotes, padding or capitalisation changed. Those replies must not be printed as
/// a translation, or every same-language line appears in chat twice.
/// </summary>
public class TextMatchTests
{
    [Theory]
    [InlineData("heyo you two! Just in time to see the show.", "\"heyo you two! Just in time to see the show.\"")]
    [InlineData("heyo you two!", "Heyo you two!")]
    [InlineData("Hello, world.", "hello world")]
    [InlineData("  spaced   out  ", "spaced out")]
    public void SameWordsInDifferentDressingCountAsUnchanged(string a, string b) =>
        Assert.True(TextMatch.Same(a, b));

    [Theory]
    [InlineData("ありがとう", "Thanks!")]
    [InlineData("heyo you two!", "heyo you two! Just in time to see the show.")]
    [InlineData("Are you coming?", "")]
    public void DifferentTextIsATranslation(string a, string b) =>
        Assert.False(TextMatch.Same(a, b));

    [Theory]
    [InlineData("lol gg, thanks for the run!")]
    [InlineData("on my way <se.1> 123")]
    [InlineData("Ça va? Danke schön, señor")]
    [InlineData("Tôi đi đây")]
    [InlineData("\uE040 Looking for party \uE041 🎉")]
    [InlineData("")]
    public void LatinScriptLinesAreSkippedForAnEnglishReader(string text) =>
        Assert.True(TextMatch.IsLatinOnly(text));

    [Theory]
    [InlineData("ありがとう")]
    [InlineData("gg ありがと")]
    [InlineData("ｗｗｗ")]
    [InlineData("안녕하세요")]
    [InlineData("привет")]
    [InlineData("零式 ok?")]
    public void AnyNonLatinLetterStillGetsTranslated(string text) =>
        Assert.False(TextMatch.IsLatinOnly(text));

    [Fact]
    public void EmptyOrSymbolOnlyTextIsNeverTreatedAsATranslationOfItself()
    {
        // Symbols normalise away, so a message of pure emoji must not silently "match" anything.
        Assert.False(TextMatch.Same("🎉", "🎉"));
    }
}
