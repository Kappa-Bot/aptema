using LightPilot.App.Presentation;
using LightPilot.Application;

namespace LightPilot.App.Tests;

public sealed class OsdPresentationControllerTests
{
    [Fact]
    public void FeedbackOsdExpiresAfterFourSeconds()
    {
        var clock = new MutableClock();
        var controller = new OsdPresentationController(clock);

        controller.Show("A little softer", canUndo: true);
        Assert.True(controller.Current.IsVisible);
        Assert.True(controller.Current.CanUndo);

        clock.Advance(TimeSpan.FromSeconds(4));
        controller.Refresh();

        Assert.False(controller.Current.IsVisible);
    }

    private sealed class MutableClock : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = new(2026, 7, 22, 20, 0, 0, TimeSpan.Zero);
        public DateTimeOffset LocalNow => UtcNow;
        public void Advance(TimeSpan value) => UtcNow += value;
    }
}
