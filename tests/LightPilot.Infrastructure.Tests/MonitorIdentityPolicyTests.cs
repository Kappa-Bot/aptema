using LightPilot.Core;
using LightPilot.Infrastructure;

namespace LightPilot.Infrastructure.Tests;

public sealed class MonitorIdentityPolicyTests
{
    [Fact]
    public void ResolveUsesEdidIdentityAcrossTopologyIndexChanges()
    {
        var first = Candidate("\\\\.\\DISPLAY1", "DISPLAY#DEL40A8#AAA", "DEL:40A8:SERIAL-7");
        var reconnected = Candidate("\\\\.\\DISPLAY3", "DISPLAY#DEL40A8#BBB", "DEL:40A8:SERIAL-7");

        var a = MonitorIdentityPolicy.Resolve(first);
        var b = MonitorIdentityPolicy.Resolve(reconnected);

        Assert.Equal(a.StableId, b.StableId);
        Assert.Contains("device:\\\\.\\DISPLAY1", a.LegacyAliases, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("device:\\\\.\\DISPLAY3", b.LegacyAliases, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveFallsBackFromTargetPathToWmiThenLegacyWithoutCollisions()
    {
        var target = MonitorIdentityPolicy.Resolve(Candidate("\\\\.\\DISPLAY1", "DISPLAY#ACR1234#A", null));
        var wmi = MonitorIdentityPolicy.Resolve(Candidate("\\\\.\\DISPLAY2", null, null) with { WmiIdentity = "ACR:1234:S2" });
        var legacy = MonitorIdentityPolicy.Resolve(Candidate("\\\\.\\DISPLAY3", null, null));

        Assert.StartsWith("display:", target.StableId);
        Assert.StartsWith("display:", wmi.StableId);
        Assert.StartsWith("display:", legacy.StableId);
        Assert.Equal(3, new[] { target.StableId, wmi.StableId, legacy.StableId }.Distinct().Count());
    }

    [Fact]
    public async Task EnumeratorDoesNotProbeOrWriteBrightnessWhenBuildingTopology()
    {
        var provider = new FakeTopologyProvider([Candidate("\\\\.\\DISPLAY1", "DISPLAY#DEL40A8#AAA", "DEL:40A8:S1")]);
        var probe = new FakeCapabilityProbe();
        var enumerator = new MonitorEnumerator(provider, probe);

        var monitors = await enumerator.EnumerateAsync(CancellationToken.None);

        var monitor = Assert.Single(monitors);
        Assert.Equal(new DisplayBounds(0, 0, 1920, 1080), monitor.Bounds);
        Assert.True(monitor.IsPrimary);
        Assert.Equal(1, probe.ReadCalls);
        Assert.Equal(0, probe.WriteCalls);
    }

    [Fact]
    public async Task EnumeratorIsolatesCapabilityFailureToOneDisplay()
    {
        var provider = new FakeTopologyProvider([
            Candidate("\\\\.\\DISPLAY1", "DISPLAY#DEL40A8#AAA", "DEL:40A8:S1"),
            Candidate("\\\\.\\DISPLAY2", "DISPLAY#ACR1234#BBB", "ACR:1234:S2") with { NativeHandle = 12 }
        ]);
        var enumerator = new MonitorEnumerator(provider, new ThrowingCapabilityProbe(11));

        var monitors = await enumerator.EnumerateAsync(CancellationToken.None);

        Assert.Equal(2, monitors.Count);
        Assert.False(monitors[0].SupportsBrightnessControl);
        Assert.True(monitors[1].SupportsBrightnessControl);
    }

    private static MonitorTopologyCandidate Candidate(string deviceName, string? targetPath, string? edid) => new(
        11, deviceName, targetPath, edid, null, "Dell U2723QE",
        new DisplayBounds(0, 0, 1920, 1080), new DisplayBounds(0, 0, 1920, 1040), true);

    private sealed class FakeTopologyProvider(IReadOnlyList<MonitorTopologyCandidate> candidates) : IMonitorTopologyProvider
    {
        public ValueTask<IReadOnlyList<MonitorTopologyCandidate>> GetTopologyAsync(CancellationToken cancellationToken) => ValueTask.FromResult(candidates);
    }

    private sealed class FakeCapabilityProbe : IMonitorCapabilityProbe
    {
        public int ReadCalls { get; private set; }
        public int WriteCalls { get; private set; }
        public bool SupportsBrightness(long nativeHandle) { ReadCalls++; return true; }
    }

    private sealed class ThrowingCapabilityProbe(long failingHandle) : IMonitorCapabilityProbe
    {
        public bool SupportsBrightness(long nativeHandle) => nativeHandle == failingHandle
            ? throw new InvalidOperationException("capability unavailable")
            : true;
    }
}
