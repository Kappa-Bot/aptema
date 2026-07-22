using LightPilot.App.Presentation;

namespace LightPilot.App.Tests;

public sealed class PlacementPolicyTests
{
    [Fact]
    public void FlyoutAnchorsAboveNotifyIconAndStaysInWorkArea()
    {
        var result = WindowPlacementPolicy.PlaceFlyout(
            new PixelRect(1850, 1040, 1880, 1070),
            new PixelRect(0, 0, 1920, 1040),
            new PixelSize(370, 500));

        Assert.Equal(new PixelPoint(1510, 530), result);
    }

    [Fact]
    public void FlyoutFallbackUsesTaskbarWorkAreaCornerDeterministically()
    {
        var result = WindowPlacementPolicy.PlaceFlyout(null, new PixelRect(0, 0, 1920, 1040), new PixelSize(370, 500));

        Assert.Equal(new PixelPoint(1538, 528), result);
    }

    [Fact]
    public void OsdUsesForegroundWindowsMonitorWorkArea()
    {
        var result = WindowPlacementPolicy.PlaceOsd(new PixelRect(1920, 0, 3840, 1040), new PixelSize(300, 100));

        Assert.Equal(new PixelPoint(3522, 18), result);
    }
}
