using Aptema.Core;

namespace Aptema.Core.Tests;

public sealed class RefreshCadencePolicyTests
{
    [Fact]
    public void PausedAppUsesSlowCadence()
    {
        var interval = RefreshCadencePolicy.GetInterval(UserSettings.Default with { AutoEnabled = false }, isPaused: true, isContentAnalysisEnabled: false);

        Assert.Equal(TimeSpan.FromSeconds(30), interval);
    }

    [Fact]
    public void NormalAppWithoutContentAnalysisUsesLowCpuCadence()
    {
        var interval = RefreshCadencePolicy.GetInterval(UserSettings.Default, isPaused: false, isContentAnalysisEnabled: false);

        Assert.Equal(TimeSpan.FromSeconds(10), interval);
    }

    [Fact]
    public void ContentAnalysisUsesShorterCadenceButNotSubsecondPolling()
    {
        var interval = RefreshCadencePolicy.GetInterval(UserSettings.Default with { EnableContentBrightnessAnalysis = true }, isPaused: false, isContentAnalysisEnabled: true);

        Assert.Equal(TimeSpan.FromSeconds(5), interval);
    }
}
