using Babel14.Services;
using Xunit;

namespace Babel14.Tests;

public class ModelsTests
{
    [Fact]
    public void EveryPresetIsSelectableAndCarriesItsCost()
    {
        foreach (var choice in Models.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(choice.Id));
            Assert.Contains(" - ", choice.Label);
            Assert.True(
                choice.Label.Contains("free") || choice.Label.Contains("per 1M"),
                $"{choice.Id} label carries no price: {choice.Label}");
        }
    }

    [Fact]
    public void TheShippedDefaultIsAPreset()
    {
        // Configuration.Model defaults to openrouter/free; if it ever stops being a preset the
        // combo silently shows "Custom" on a fresh install.
        Assert.Equal(0, Models.IndexOf("openrouter/free"));
    }

    [Fact]
    public void CustomSitsAfterThePresets()
    {
        Assert.Equal(Models.All.Length, Models.CustomIndex);
        Assert.Equal(Models.All.Length + 1, Models.Labels.Length);
        Assert.StartsWith("Custom", Models.Labels[Models.CustomIndex]);
    }

    [Fact]
    public void AnUnknownSlugIsCustom()
    {
        Assert.Equal(-1, Models.IndexOf("someone/unknown-model"));
        Assert.Equal(-1, Models.IndexOf(""));
        Assert.Equal(-1, Models.IndexOf(null));
        Assert.Equal(1, Models.IndexOf(Models.All[1].Id));
    }

    [Fact]
    public void ExactlyOneModelIsRecommended() =>
        Assert.Single(Models.All, m => m.Label.Contains("recommended"));
}
