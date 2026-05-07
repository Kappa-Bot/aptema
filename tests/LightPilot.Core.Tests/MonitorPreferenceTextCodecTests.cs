using LightPilot.Core;

namespace LightPilot.Core.Tests;

public sealed class MonitorPreferenceTextCodecTests
{
    [Fact]
    public void ParseReadsOffsetAndDisabledRules()
    {
        var preferences = MonitorPreferenceTextCodec.Parse("""
            device:\\.\DISPLAY1=offset:+5
            device:\\.\DISPLAY2=off
            """);

        Assert.Equal(5, preferences[0].BrightnessOffsetPercent);
        Assert.False(preferences[0].IsDisabled);
        Assert.True(preferences[1].IsDisabled);
    }

    [Fact]
    public void ParseIgnoresInvalidLines()
    {
        var preferences = MonitorPreferenceTextCodec.Parse("""
            bad-line
            display=offset:nope
            valid=offset:-4
            """);

        Assert.Single(preferences);
        Assert.Equal("valid", preferences[0].MonitorId);
        Assert.Equal(-4, preferences[0].BrightnessOffsetPercent);
    }

    [Fact]
    public void FormatWritesStableRules()
    {
        var text = MonitorPreferenceTextCodec.Format(new[]
        {
            new MonitorPreference { MonitorId = "b", BrightnessOffsetPercent = 4 },
            new MonitorPreference { MonitorId = "a", IsDisabled = true }
        });

        Assert.Equal("a=off" + Environment.NewLine + "b=offset:+4", text);
    }
}
