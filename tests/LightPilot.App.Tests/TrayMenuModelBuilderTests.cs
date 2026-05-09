using LightPilot.App.Services;

namespace LightPilot.App.Tests;

public sealed class TrayMenuModelBuilderTests
{
    [Fact]
    public void BuildsPremiumTrayOrder()
    {
        var state = new TrayMenuState(AutoEnabled: true, IsPaused: false, ComfortState: "Comfortable now", Mode: "Soft evening light", NextAdaptationText: "Next check in 20 sec");

        var items = TrayMenuModelBuilder.Build(state);

        Assert.Equal(
            new[]
            {
                TrayMenuCommandKey.ToggleAuto,
                TrayMenuCommandKey.PauseThirtyMinutes,
                TrayMenuCommandKey.PauseUntilTomorrow,
                TrayMenuCommandKey.TooBright,
                TrayMenuCommandKey.TooDim,
                TrayMenuCommandKey.Perfect,
                TrayMenuCommandKey.Open,
                TrayMenuCommandKey.Settings,
                TrayMenuCommandKey.Exit
            },
            items.Where(item => !item.IsSeparator).Select(item => item.CommandKey));
    }

    [Fact]
    public void TooltipIncludesStateModeAndNextAdaptationWithinNotifyIconLimit()
    {
        var state = new TrayMenuState(AutoEnabled: true, IsPaused: false, ComfortState: "Comfortable now", Mode: "Soft evening light", NextAdaptationText: "Next check in 20 sec");

        var tooltip = TrayMenuModelBuilder.BuildTooltip(state);

        Assert.Contains("Comfortable", tooltip);
        Assert.Contains("Soft", tooltip);
        Assert.True(tooltip.Length <= 63);
    }

    [Fact]
    public void PausedTooltipDoesNotPromiseUpcomingAdaptation()
    {
        var state = new TrayMenuState(AutoEnabled: true, IsPaused: true, ComfortState: "Paused", Mode: "Manual pause", NextAdaptationText: "Next check in 20 sec");

        var tooltip = TrayMenuModelBuilder.BuildTooltip(state);

        Assert.DoesNotContain("Next", tooltip);
        Assert.Contains("Paused", tooltip);
    }
}
