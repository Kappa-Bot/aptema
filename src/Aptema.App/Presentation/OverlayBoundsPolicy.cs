using Aptema.Core;

namespace Aptema.App.Presentation;

public readonly record struct PixelRectangle(int X, int Y, int Width, int Height);

public static class OverlayBoundsPolicy
{
    public static bool TryGetPhysicalBounds(DisplayBounds bounds, out PixelRectangle rectangle)
    {
        if (!bounds.IsValid)
        {
            rectangle = default;
            return false;
        }

        rectangle = new PixelRectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        return true;
    }
}
