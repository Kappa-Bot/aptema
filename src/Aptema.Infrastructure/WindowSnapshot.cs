namespace Aptema.Infrastructure;

public readonly record struct RectInt(int X, int Y, int Width, int Height)
{
    public int Left => X;
    public int Top => Y;
    public int Right => X + Width;
    public int Bottom => Y + Height;
}

public sealed record WindowSnapshot(
    nint Handle,
    string? ProcessName,
    RectInt Bounds,
    RectInt MonitorBounds,
    RectInt WorkArea,
    bool IsMaximized);
