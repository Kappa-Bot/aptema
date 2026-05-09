using LightPilot.Core;
using LightPilot.Infrastructure;

namespace LightPilot.Infrastructure.Tests;

public sealed class BrightnessControllerTests
{
    [Fact]
    public async Task UsesDdcCiWhenSupportedAndEnabled()
    {
        var ddc = new FakeDdcCiApi(canSet: true);
        var windows = new FakeWindowsBrightnessApi(canSet: true);
        var overlay = new FakeOverlayController();
        var controller = new BrightnessController(ddc, windows, overlay, TimeProvider.System);
        var monitor = Monitor with { SupportsBrightnessControl = true };

        var result = await controller.ApplyAsync(monitor, Decision(52), UserSettings.Default, CancellationToken.None);

        Assert.Equal(52, ddc.LastBrightness);
        Assert.Null(windows.LastBrightness);
        Assert.Equal(0, overlay.LastOpacity);
        Assert.Equal(MonitorControlState.Ready, result.State);
        Assert.Equal(BrightnessControlLayer.DdcCi, result.AppliedLayer);
    }

    [Fact]
    public async Task AppliesWarmOverlayWhenHardwareBrightnessSucceeds()
    {
        var ddc = new FakeDdcCiApi(canSet: true);
        var overlay = new FakeOverlayController();
        var controller = new BrightnessController(ddc, new FakeWindowsBrightnessApi(canSet: false), overlay, TimeProvider.System);
        var monitor = Monitor with { SupportsBrightnessControl = true };

        var result = await controller.ApplyAsync(monitor, Decision(52, overlayOpacity: 0.08), UserSettings.Default, CancellationToken.None);

        Assert.Equal(52, ddc.LastBrightness);
        Assert.Equal(0.08, overlay.LastOpacity);
        Assert.True(result.UsedHardware);
        Assert.True(result.UsedOverlay);
    }

    [Fact]
    public async Task FallsBackToWindowsBrightnessThenOverlay()
    {
        var ddc = new FakeDdcCiApi(canSet: false);
        var windows = new FakeWindowsBrightnessApi(canSet: false);
        var overlay = new FakeOverlayController();
        var controller = new BrightnessController(ddc, windows, overlay, TimeProvider.System);

        var result = await controller.ApplyAsync(Monitor, Decision(45, overlayOpacity: 0.2), UserSettings.Default, CancellationToken.None);

        Assert.Equal(0.2, overlay.LastOpacity);
        Assert.Equal(MonitorControlState.FallbackUsed, result.State);
        Assert.Equal(BrightnessControlLayer.Overlay, result.AppliedLayer);
    }

    [Fact]
    public async Task ThrottlesWritesWithinTwoSecondsPerMonitor()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 5, 7, 12, 0, 0, TimeSpan.Zero));
        var ddc = new FakeDdcCiApi(canSet: true);
        var controller = new BrightnessController(ddc, new FakeWindowsBrightnessApi(canSet: true), new FakeOverlayController(), time);

        await controller.ApplyAsync(Monitor, Decision(50), UserSettings.Default, CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(1));
        var result = await controller.ApplyAsync(Monitor, Decision(55), UserSettings.Default, CancellationToken.None);

        Assert.Equal(1, ddc.WriteCount);
        Assert.Equal(50, ddc.LastBrightness);
        Assert.Equal(MonitorControlState.Throttled, result.State);
    }

    [Fact]
    public async Task BacksOffDdcAfterRepeatedFailures()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 5, 7, 12, 0, 0, TimeSpan.Zero));
        var ddc = new FakeDdcCiApi(canSet: false);
        var windows = new FakeWindowsBrightnessApi(canSet: true);
        var controller = new BrightnessController(ddc, windows, new FakeOverlayController(), time);
        var monitor = Monitor with { SupportsBrightnessControl = true };

        for (var i = 0; i < 3; i++)
        {
            await controller.ApplyAsync(monitor, Decision(50 + i), UserSettings.Default, CancellationToken.None);
            time.Advance(TimeSpan.FromSeconds(3));
        }

        var result = await controller.ApplyAsync(monitor, Decision(60), UserSettings.Default, CancellationToken.None);

        Assert.Equal(3, ddc.WriteCount);
        Assert.Equal(60, windows.LastBrightness);
        Assert.Equal(MonitorControlState.Degraded, result.State);
        Assert.Equal(BrightnessControlLayer.WindowsBrightness, result.AppliedLayer);
        Assert.NotNull(result.SuppressedUntil);
    }

    [Fact]
    public async Task ProtectedDecisionDoesNotWriteHardware()
    {
        var ddc = new FakeDdcCiApi(canSet: true);
        var windows = new FakeWindowsBrightnessApi(canSet: true);
        var overlay = new FakeOverlayController();
        var controller = new BrightnessController(ddc, windows, overlay, TimeProvider.System);

        var result = await controller.ApplyAsync(Monitor, Decision(50, shouldApply: false, source: DecisionSource.Protected), UserSettings.Default, CancellationToken.None);

        Assert.Equal(MonitorControlState.Protected, result.State);
        Assert.Null(ddc.LastBrightness);
        Assert.Null(windows.LastBrightness);
        Assert.Null(overlay.LastOpacity);
    }

    [Fact]
    public async Task DisabledMonitorReturnsDisabledWithoutWrites()
    {
        var ddc = new FakeDdcCiApi(canSet: true);
        var controller = new BrightnessController(ddc, new FakeWindowsBrightnessApi(canSet: true), new FakeOverlayController(), TimeProvider.System);
        var settings = UserSettings.Default with
        {
            MonitorPreferences = new[]
            {
                new MonitorPreference { MonitorId = "m1", IsDisabled = true }
            }
        };

        var result = await controller.ApplyAsync(Monitor, Decision(50), settings, CancellationToken.None);

        Assert.Equal(MonitorControlState.Disabled, result.State);
        Assert.Null(ddc.LastBrightness);
    }

    [Fact]
    public async Task NativeFailureFallsBackToOverlayWithoutThrowing()
    {
        var ddc = new ThrowingDdcCiApi();
        var windows = new ThrowingWindowsBrightnessApi();
        var overlay = new FakeOverlayController();
        var controller = new BrightnessController(ddc, windows, overlay, TimeProvider.System);

        var result = await controller.ApplyAsync(Monitor, Decision(45, overlayOpacity: 0.12), UserSettings.Default, CancellationToken.None);

        Assert.Equal(MonitorControlState.Failed, result.State);
        Assert.Equal(BrightnessControlLayer.Overlay, result.AppliedLayer);
        Assert.Equal("HardwareControlFailed", result.ReasonCode);
        Assert.Equal(0.12, overlay.LastOpacity);
    }

    [Fact]
    public async Task OverlayFailureReturnsFailedWithoutThrowing()
    {
        var controller = new BrightnessController(
            new FakeDdcCiApi(canSet: false),
            new FakeWindowsBrightnessApi(canSet: false),
            new ThrowingOverlayController(),
            TimeProvider.System);

        var result = await controller.ApplyAsync(Monitor, Decision(45, overlayOpacity: 0.12), UserSettings.Default, CancellationToken.None);

        Assert.Equal(MonitorControlState.Failed, result.State);
        Assert.Equal("OverlayControlFailed", result.ReasonCode);
    }

    private static MonitorModel Monitor => new("m1", "Desk", true, true, 15, 100, 0);

    private static ComfortDecision Decision(int brightness, double overlayOpacity = 0, bool shouldApply = true, DecisionSource source = DecisionSource.Default)
    {
        return new ComfortDecision(ComfortProfileId.Evening, brightness, 4200, overlayOpacity, TimeSpan.FromSeconds(45), shouldApply, "test", Array.Empty<string>(), Source: source);
    }
}

internal sealed class FakeDdcCiApi(bool canSet) : IDdcCiApi
{
    public int? LastBrightness { get; private set; }
    public int WriteCount { get; private set; }

    public ValueTask<bool> TrySetBrightnessAsync(MonitorModel monitor, int brightnessPercent, CancellationToken cancellationToken)
    {
        WriteCount++;
        if (canSet)
        {
            LastBrightness = brightnessPercent;
        }

        return ValueTask.FromResult(canSet);
    }
}

internal sealed class FakeWindowsBrightnessApi(bool canSet) : IWindowsBrightnessApi
{
    public int? LastBrightness { get; private set; }

    public ValueTask<bool> TrySetBrightnessAsync(MonitorModel monitor, int brightnessPercent, CancellationToken cancellationToken)
    {
        if (canSet)
        {
            LastBrightness = brightnessPercent;
        }

        return ValueTask.FromResult(canSet);
    }
}

internal sealed class FakeOverlayController : IOverlayController
{
    public double? LastOpacity { get; private set; }

    public ValueTask ApplyAsync(MonitorModel monitor, double opacity, int colorTemperatureKelvin, CancellationToken cancellationToken)
    {
        LastOpacity = opacity;
        return ValueTask.CompletedTask;
    }
}

internal sealed class ThrowingOverlayController : IOverlayController
{
    public ValueTask ApplyAsync(MonitorModel monitor, double opacity, int colorTemperatureKelvin, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("overlay failed");
    }
}

internal sealed class ThrowingDdcCiApi : IDdcCiApi
{
    public ValueTask<bool> TrySetBrightnessAsync(MonitorModel monitor, int brightnessPercent, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("native failed");
    }
}

internal sealed class ThrowingWindowsBrightnessApi : IWindowsBrightnessApi
{
    public ValueTask<bool> TrySetBrightnessAsync(MonitorModel monitor, int brightnessPercent, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("wmi failed");
    }
}

internal sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by)
    {
        _now += by;
    }
}
