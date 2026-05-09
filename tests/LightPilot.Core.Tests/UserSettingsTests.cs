using LightPilot.Core;

namespace LightPilot.Core.Tests;

public sealed class UserSettingsTests
{
    [Fact]
    public void DefaultsFavorGentleComfort()
    {
        Assert.Equal(3, UserSettings.Default.SchemaVersion);
        Assert.Equal(45, UserSettings.Default.ComfortIntensity);
        Assert.Equal(25, UserSettings.Default.MinimumBrightnessPercent);
        Assert.Equal(90, UserSettings.Default.MaximumBrightnessPercent);
        Assert.Equal(TimeSpan.FromSeconds(90), UserSettings.Default.TransitionSpeed);
        Assert.True(UserSettings.Default.EnablePreferenceLearning);
        Assert.Empty(UserSettings.Default.PreferenceLearning.Aggregates);
    }
}
