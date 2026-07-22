using LightPilot.Application;

namespace LightPilot.App.Presentation;

public sealed record OsdPresentationState(string Message, bool IsVisible, bool CanUndo, DateTimeOffset HideAt)
{
    public static OsdPresentationState Hidden { get; } = new(string.Empty, false, false, DateTimeOffset.MinValue);
}

public sealed class OsdPresentationController(IClock clock)
{
    public OsdPresentationState Current { get; private set; } = OsdPresentationState.Hidden;

    public void Show(string message, bool canUndo)
    {
        Current = new OsdPresentationState(message, true, canUndo, clock.UtcNow.AddSeconds(4));
    }

    public void Refresh()
    {
        if (Current.IsVisible && clock.UtcNow >= Current.HideAt)
        {
            Current = Current with { IsVisible = false };
        }
    }

    public void Dismiss() => Current = Current with { IsVisible = false };
}
