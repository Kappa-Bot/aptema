using Aptema.Core;

namespace Aptema.Core.Tests;

public sealed class ComfortPreferenceAdvisorTests
{
    [Fact]
    public void TooBrightIncreasesComfortGently()
    {
        var settings = UserSettings.Default with { ComfortIntensity = 45 };

        var updated = ComfortPreferenceAdvisor.Apply(settings, ComfortFeedback.TooBright);

        Assert.Equal(50, updated.ComfortIntensity);
        Assert.True(updated.AutoEnabled);
    }

    [Fact]
    public void TooDimDecreasesComfortGently()
    {
        var settings = UserSettings.Default with { ComfortIntensity = 45 };

        var updated = ComfortPreferenceAdvisor.Apply(settings, ComfortFeedback.TooDim);

        Assert.Equal(40, updated.ComfortIntensity);
    }

    [Fact]
    public void FeedbackStaysInsideSafeRange()
    {
        var low = UserSettings.Default with { ComfortIntensity = 0 };
        var high = UserSettings.Default with { ComfortIntensity = 100 };

        Assert.Equal(0, ComfortPreferenceAdvisor.Apply(low, ComfortFeedback.TooDim).ComfortIntensity);
        Assert.Equal(100, ComfortPreferenceAdvisor.Apply(high, ComfortFeedback.TooBright).ComfortIntensity);
    }

    [Fact]
    public void ContextualFeedbackStoresLearningSignal()
    {
        var settings = UserSettings.Default;
        var context = new PreferenceLearningContext("monitor-1", AppCategory.Browser, DayPhase.Night, false, LuminanceClassification.MostlyWhite);

        var updated = ComfortPreferenceAdvisor.Apply(settings, ComfortFeedback.TooWarm, context, new DateTimeOffset(2026, 5, 8, 23, 0, 0, TimeSpan.Zero));

        var adjustment = PreferenceLearningService.GetAdjustment(updated.PreferenceLearning, context);
        Assert.True(updated.AutoEnabled);
        Assert.True(adjustment.IsLearned);
        Assert.Equal(80, adjustment.WarmthOffsetKelvin);
    }

    [Fact]
    public void PerfectFeedbackDoesNotMoveGlobalIntensity()
    {
        var settings = UserSettings.Default with { ComfortIntensity = 45 };

        var updated = ComfortPreferenceAdvisor.Apply(settings, ComfortFeedback.Perfect);

        Assert.Equal(45, updated.ComfortIntensity);
        Assert.True(updated.AutoEnabled);
    }
}
