namespace Aptema.App.Presentation;

public static class MotionPolicy
{
    public static TimeSpan GetTransitionDuration(bool reducedMotion) =>
        reducedMotion ? TimeSpan.Zero : TimeSpan.FromMilliseconds(160);
}
