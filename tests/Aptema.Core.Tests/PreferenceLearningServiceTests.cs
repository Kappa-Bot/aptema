using Aptema.Core;

namespace Aptema.Core.Tests;

public sealed class PreferenceLearningServiceTests
{
    [Fact]
    public void TooBrightLearnsDarkerWarmerOffsetForSameContext()
    {
        var model = PreferenceLearningModel.Empty;
        var context = Context(AppCategory.Browser, DayPhase.Night, LuminanceClassification.MostlyWhite);
        var now = new DateTimeOffset(2026, 5, 8, 23, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < 3; i++)
        {
            model = PreferenceLearningService.RecordFeedback(model, context, ComfortFeedback.TooBright, now.AddMinutes(i));
        }

        var adjustment = PreferenceLearningService.GetAdjustment(model, context);

        Assert.True(adjustment.IsLearned);
        Assert.Equal(-6, adjustment.BrightnessOffsetPercent);
        Assert.Equal(-240, adjustment.WarmthOffsetKelvin);
        Assert.InRange(adjustment.Confidence, 0.49, 0.51);
    }

    [Fact]
    public void MixedFeedbackReducesConfidenceAndNetOffset()
    {
        var model = PreferenceLearningModel.Empty;
        var context = Context(AppCategory.Development, DayPhase.Night, LuminanceClassification.Dark);
        var now = new DateTimeOffset(2026, 5, 8, 1, 0, 0, TimeSpan.Zero);

        model = PreferenceLearningService.RecordFeedback(model, context, ComfortFeedback.TooBright, now);
        model = PreferenceLearningService.RecordFeedback(model, context, ComfortFeedback.TooBright, now.AddMinutes(1));
        model = PreferenceLearningService.RecordFeedback(model, context, ComfortFeedback.TooDim, now.AddMinutes(2));

        var adjustment = PreferenceLearningService.GetAdjustment(model, context);

        Assert.Equal(-2, adjustment.BrightnessOffsetPercent);
        Assert.Equal(-80, adjustment.WarmthOffsetKelvin);
        Assert.InRange(adjustment.Confidence, 0.16, 0.18);
    }

    [Fact]
    public void PerfectRaisesConfidenceWithoutOffsetJump()
    {
        var model = PreferenceLearningModel.Empty;
        var context = Context(AppCategory.OfficeReading, DayPhase.Evening, LuminanceClassification.Balanced);
        var now = new DateTimeOffset(2026, 5, 8, 19, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < 6; i++)
        {
            model = PreferenceLearningService.RecordFeedback(model, context, ComfortFeedback.Perfect, now.AddMinutes(i));
        }

        var adjustment = PreferenceLearningService.GetAdjustment(model, context);

        Assert.True(adjustment.IsLearned);
        Assert.Equal(0, adjustment.BrightnessOffsetPercent);
        Assert.Equal(0, adjustment.WarmthOffsetKelvin);
        Assert.InRange(adjustment.Confidence, 0.74, 0.76);
    }

    [Fact]
    public void LearningOnlyAppliesToMatchingContext()
    {
        var model = PreferenceLearningModel.Empty;
        var learned = Context(AppCategory.Browser, DayPhase.Night, LuminanceClassification.MostlyWhite);
        var other = Context(AppCategory.Browser, DayPhase.Day, LuminanceClassification.MostlyWhite);

        model = PreferenceLearningService.RecordFeedback(model, learned, ComfortFeedback.TooBright, DateTimeOffset.UtcNow);

        Assert.False(PreferenceLearningService.GetAdjustment(model, other).IsLearned);
    }

    [Fact]
    public void AggregateCapKeepsNewestTwoHundredContexts()
    {
        var model = PreferenceLearningModel.Empty;
        var start = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < 205; i++)
        {
            var context = new PreferenceLearningContext($"monitor-{i}", AppCategory.Browser, DayPhase.Day, false, LuminanceClassification.Bright);
            model = PreferenceLearningService.RecordFeedback(model, context, ComfortFeedback.TooBright, start.AddMinutes(i));
        }

        Assert.Equal(200, model.Aggregates.Count);
        Assert.DoesNotContain(model.Aggregates, aggregate => aggregate.MonitorId == "monitor-0");
        Assert.Contains(model.Aggregates, aggregate => aggregate.MonitorId == "monitor-204");
    }

    private static PreferenceLearningContext Context(AppCategory category, DayPhase phase, LuminanceClassification luminance)
    {
        return new PreferenceLearningContext("monitor-1", category, phase, IsFullscreen: false, luminance);
    }
}
