using LightPilot.Application;
using LightPilot.Core;

namespace LightPilot.Application.Tests;

public sealed class ContractsTests
{
    [Fact]
    public void OperationResultTreatsDegradedValueAsUsable()
    {
        var result = OperationResult<string>.Degraded("usable", "fallback");

        Assert.True(result.IsUsable);
        Assert.Equal(OperationStatus.Degraded, result.Status);
        Assert.Equal("usable", result.Value);
        Assert.Equal("fallback", result.Code);
    }

    [Fact]
    public void RuntimeSnapshotIsImmutableAndSummarizesLearning()
    {
        var now = new DateTimeOffset(2026, 7, 22, 18, 0, 0, TimeSpan.Zero);
        var snapshot = ComfortRuntimeSnapshot.Empty(now) with
        {
            Learning = new LearningSummary(2, 7, 0.75)
        };

        Assert.Equal(now, snapshot.CapturedAt);
        Assert.Equal(2, snapshot.Learning.ContextCount);
        Assert.Empty(snapshot.Displays);
    }

    [Fact]
    public void TimeProviderClockUsesInjectedTimeProvider()
    {
        var expected = new DateTimeOffset(2026, 7, 22, 18, 30, 0, TimeSpan.Zero);
        var clock = new TimeProviderClock(new FixedTimeProvider(expected));

        Assert.Equal(expected, clock.UtcNow);
    }

    [Fact]
    public void RuntimeSnapshotCopiesMutableCollections()
    {
        var monitor = new MonitorModel("display-1", "Display 1", false, true, 15, 100, 0);
        var decision = new ComfortDecision(ComfortProfileId.Day, 60, 5200, 0, TimeSpan.Zero, true, "Day", ["Day"]);
        var displays = new List<DisplayRuntimeState>
        {
            new(monitor, decision, BrightnessApplyResult.NoChange(monitor.Id))
        };
        var issues = new List<string> { "first" };
        var snapshot = new ComfortRuntimeSnapshot(
            DateTimeOffset.UtcNow,
            new AppContextModel("test.exe", AppCategory.Unknown, false),
            ContentLuminanceSample.Unknown,
            displays,
            decision,
            null,
            LearningSummary.Empty,
            new SystemHealthState(true, issues));

        displays.Clear();
        issues.Add("second");

        Assert.Single(snapshot.Displays);
        Assert.Equal("first", Assert.Single(snapshot.Health.Issues));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
