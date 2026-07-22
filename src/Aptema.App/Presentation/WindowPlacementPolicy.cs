namespace Aptema.App.Presentation;

public readonly record struct PixelPoint(int X, int Y);

public readonly record struct PixelSize(int Width, int Height);

public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Math.Max(0, Right - Left);
    public int Height => Math.Max(0, Bottom - Top);
    public int CenterX => Left + Width / 2;
    public int CenterY => Top + Height / 2;
}

public static class WindowPlacementPolicy
{
    private const int EdgeMargin = 8;
    private const int AnchorGap = 10;
    private const int FallbackMargin = 12;
    private const int OsdMargin = 18;

    public static PixelPoint PlaceFlyout(PixelRect? notifyIcon, PixelRect workArea, PixelSize flyout)
    {
        if (notifyIcon is not { } icon)
        {
            return Clamp(
                workArea.Right - flyout.Width - FallbackMargin,
                workArea.Bottom - flyout.Height - FallbackMargin,
                workArea,
                flyout,
                FallbackMargin);
        }

        int left;
        int top;
        if (icon.Left >= workArea.Right)
        {
            left = icon.Left - flyout.Width - AnchorGap;
            top = icon.Bottom - flyout.Height;
        }
        else if (icon.Right <= workArea.Left)
        {
            left = icon.Right + AnchorGap;
            top = icon.Bottom - flyout.Height;
        }
        else if (icon.CenterY >= workArea.Top + workArea.Height / 2)
        {
            left = icon.Right - flyout.Width;
            top = icon.Top - flyout.Height - AnchorGap;
        }
        else
        {
            left = icon.Right - flyout.Width;
            top = icon.Bottom + AnchorGap;
        }

        return Clamp(left, top, workArea, flyout, EdgeMargin);
    }

    public static PixelPoint PlaceOsd(PixelRect workArea, PixelSize osd) =>
        Clamp(
            workArea.Right - osd.Width - OsdMargin,
            workArea.Top + OsdMargin,
            workArea,
            osd,
            OsdMargin);

    private static PixelPoint Clamp(int left, int top, PixelRect workArea, PixelSize size, int margin)
    {
        var minimumX = workArea.Left + margin;
        var minimumY = workArea.Top + margin;
        var maximumX = Math.Max(minimumX, workArea.Right - size.Width - margin);
        var maximumY = Math.Max(minimumY, workArea.Bottom - size.Height - margin);
        return new PixelPoint(Math.Clamp(left, minimumX, maximumX), Math.Clamp(top, minimumY, maximumY));
    }
}
