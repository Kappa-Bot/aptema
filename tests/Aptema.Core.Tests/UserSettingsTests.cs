using Aptema.Core;

namespace Aptema.Core.Tests;

public sealed class UserSettingsTests
{
    [Fact]
    public void StableDisplayReconciliationRekeysLegacyLearningWithoutLosingSamples()
    {
        var context = new PreferenceLearningContext("device:\\\\.\\DISPLAY1", AppCategory.Browser, DayPhase.Night, false, LuminanceClassification.Bright);
        var learning = PreferenceLearningService.RecordFeedback(PreferenceLearningModel.Empty, context, ComfortFeedback.TooBright, DateTimeOffset.Parse("2026-07-20T22:00:00Z"));
        var settings = UserSettings.Default with
        {
            PreferenceLearning = learning,
            DisplayConfigurations = [new DisplayConfiguration("legacy-hash", ["device:\\\\.\\DISPLAY1"], true, -3, 25, 90, true)]
        };
        var monitor = new MonitorModel("display:edid-stable", "Dell", true, true, 15, 100, 0, 11,
            new DisplayBounds(0, 0, 1920, 1080), new DisplayBounds(0, 0, 1920, 1040), true,
            ["device:\\\\.\\DISPLAY1"], "Dell U2723QE");

        var reconciled = StableDisplaySettingsReconciler.Reconcile(settings, [monitor]);

        Assert.Equal("display:edid-stable", reconciled.DisplayConfigurations[0].StableId);
        var aggregate = Assert.Single(reconciled.PreferenceLearning.Aggregates);
        Assert.Equal("display:edid-stable", aggregate.MonitorId);
        Assert.Equal(1, aggregate.Samples);
        Assert.StartsWith("DISPLAY:EDID-STABLE|", aggregate.Key);
    }
    [Fact]
    public void DefaultsFavorGentleComfort()
    {
        Assert.Equal(6, UserSettings.Default.SchemaVersion);
        Assert.Equal(45, UserSettings.Default.ComfortIntensity);
        Assert.Equal(25, UserSettings.Default.MinimumBrightnessPercent);
        Assert.Equal(90, UserSettings.Default.MaximumBrightnessPercent);
        Assert.Equal(TimeSpan.FromSeconds(90), UserSettings.Default.TransitionSpeed);
        Assert.True(UserSettings.Default.EnablePreferenceLearning);
        Assert.Empty(UserSettings.Default.PreferenceLearning.Aggregates);
        Assert.Equal("Win+Alt+A", UserSettings.Default.Hotkeys.QuickAdjust);
        Assert.Empty(UserSettings.Default.DisplayConfigurations);
    }
}
