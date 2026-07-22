using Aptema.Core;

namespace Aptema.Core.Tests;

public sealed class PowerAwareSettingsPolicyTests
{
    [Fact]
    public void OnBatteryDisablesContentAnalysisAndSlowsTransitions()
    {
        var settings = UserSettings.Default with
        {
            EnableContentBrightnessAnalysis = true,
            TransitionSpeed = TimeSpan.FromSeconds(45),
            ReduceWorkOnBattery = true
        };

        var effective = PowerAwareSettingsPolicy.Apply(settings, isOnBattery: true);

        Assert.False(effective.EnableContentBrightnessAnalysis);
        Assert.Equal(TimeSpan.FromSeconds(120), effective.TransitionSpeed);
    }

    [Fact]
    public void PluggedInLeavesSettingsAlone()
    {
        var settings = UserSettings.Default with
        {
            EnableContentBrightnessAnalysis = true,
            TransitionSpeed = TimeSpan.FromSeconds(60),
            ReduceWorkOnBattery = true
        };

        var effective = PowerAwareSettingsPolicy.Apply(settings, isOnBattery: false);

        Assert.Equal(settings, effective);
    }

    [Fact]
    public void UserCanDisableBatteryReduction()
    {
        var settings = UserSettings.Default with
        {
            EnableContentBrightnessAnalysis = true,
            TransitionSpeed = TimeSpan.FromSeconds(60),
            ReduceWorkOnBattery = false
        };

        var effective = PowerAwareSettingsPolicy.Apply(settings, isOnBattery: true);

        Assert.Equal(settings, effective);
    }
}
