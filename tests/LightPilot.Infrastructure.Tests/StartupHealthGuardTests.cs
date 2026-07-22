using LightPilot.Infrastructure;

namespace LightPilot.Infrastructure.Tests;

public sealed class StartupHealthGuardTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AptemaStartupTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void RepeatedIncompleteStartsEnableSafeMode()
    {
        var path = Path.Combine(_root, "startup-health.json");

        new StartupHealthGuard(path).BeginStartup();
        new StartupHealthGuard(path).BeginStartup();
        new StartupHealthGuard(path).BeginStartup();
        var result = new StartupHealthGuard(path).BeginStartup();

        Assert.True(result.ShouldStartSafeMode);
        Assert.Equal(3, result.ConsecutiveFailures);
    }

    [Fact]
    public void HealthyStartupClearsFailureSequence()
    {
        var path = Path.Combine(_root, "startup-health.json");
        var guard = new StartupHealthGuard(path);
        guard.BeginStartup();
        new StartupHealthGuard(path).BeginStartup();

        guard.MarkHealthy();
        var result = new StartupHealthGuard(path).BeginStartup();

        Assert.False(result.ShouldStartSafeMode);
        Assert.Equal(0, result.ConsecutiveFailures);
    }

    [Fact]
    public void CorruptStateFailsIntoSafeModeWithoutThrowing()
    {
        var path = Path.Combine(_root, "startup-health.json");
        Directory.CreateDirectory(_root);
        File.WriteAllText(path, "not-json");

        var result = new StartupHealthGuard(path).BeginStartup();

        Assert.True(result.ShouldStartSafeMode);
        Assert.Equal("StartupStateRecovered", result.ReasonCode);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
