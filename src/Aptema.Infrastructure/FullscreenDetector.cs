namespace Aptema.Infrastructure;

public sealed class FullscreenDetector : IFullscreenDetector
{
    private readonly int _tolerancePixels;

    public FullscreenDetector(int tolerancePixels = 6)
    {
        _tolerancePixels = tolerancePixels;
    }

    public bool IsFullscreen(WindowSnapshot snapshot)
    {
        if (snapshot.IsMaximized && ApproximatelyEqual(snapshot.Bounds, snapshot.WorkArea))
        {
            return false;
        }

        return ApproximatelyEqual(snapshot.Bounds, snapshot.MonitorBounds);
    }

    private bool ApproximatelyEqual(RectInt actual, RectInt expected)
    {
        return Math.Abs(actual.Left - expected.Left) <= _tolerancePixels
            && Math.Abs(actual.Top - expected.Top) <= _tolerancePixels
            && Math.Abs(actual.Right - expected.Right) <= _tolerancePixels
            && Math.Abs(actual.Bottom - expected.Bottom) <= _tolerancePixels;
    }
}
