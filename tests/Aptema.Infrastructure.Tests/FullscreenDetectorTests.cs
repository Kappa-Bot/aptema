using Aptema.Infrastructure;

namespace Aptema.Infrastructure.Tests;

public sealed class FullscreenDetectorTests
{
    [Fact]
    public void WindowCoveringMonitorBoundsIsFullscreen()
    {
        var detector = new FullscreenDetector();
        var snapshot = new WindowSnapshot(
            Handle: 1,
            ProcessName: "vlc.exe",
            Bounds: new RectInt(0, 0, 1920, 1080),
            MonitorBounds: new RectInt(0, 0, 1920, 1080),
            WorkArea: new RectInt(0, 0, 1920, 1040),
            IsMaximized: false);

        Assert.True(detector.IsFullscreen(snapshot));
    }

    [Fact]
    public void MaximizedWindowCoveringWorkAreaIsNotFullscreen()
    {
        var detector = new FullscreenDetector();
        var snapshot = new WindowSnapshot(
            Handle: 1,
            ProcessName: "chrome.exe",
            Bounds: new RectInt(0, 0, 1920, 1040),
            MonitorBounds: new RectInt(0, 0, 1920, 1080),
            WorkArea: new RectInt(0, 0, 1920, 1040),
            IsMaximized: true);

        Assert.False(detector.IsFullscreen(snapshot));
    }

    [Fact]
    public void SmallDpiFrameToleranceStillCountsAsFullscreen()
    {
        var detector = new FullscreenDetector(tolerancePixels: 8);
        var snapshot = new WindowSnapshot(
            Handle: 1,
            ProcessName: "game.exe",
            Bounds: new RectInt(-4, -4, 1928, 1088),
            MonitorBounds: new RectInt(0, 0, 1920, 1080),
            WorkArea: new RectInt(0, 0, 1920, 1040),
            IsMaximized: false);

        Assert.True(detector.IsFullscreen(snapshot));
    }
}
