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
                TrayMenuCommandKey.Warmer,
                TrayMenuCommandKey.Cooler,
                TrayMenuCommandKey.Perfect,
                TrayMenuCommandKey.Open,
                TrayMenuCommandKey.Settings,
                TrayMenuCommandKey.Exit
            },
            items.Where(item => !item.IsSeparator).Select(item => item.CommandKey));
        Assert.Contains(items, item => item.Text == "Open Aptema");
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

    [Theory]
    [InlineData(false, false, false, TrayIconState.Active)]
    [InlineData(true, false, false, TrayIconState.Paused)]
    [InlineData(false, true, false, TrayIconState.Degraded)]
    [InlineData(false, true, true, TrayIconState.Error)]
    public void ResolvesTrayIconState(bool paused, bool degraded, bool error, TrayIconState expected)
    {
        Assert.Equal(expected, TrayPresentation.ResolveIconState(paused, degraded, error));
    }

    [Theory]
    [InlineData(TrayIconState.Active, TrayIconState.Active, false)]
    [InlineData(TrayIconState.Paused, TrayIconState.Paused, false)]
    [InlineData(TrayIconState.Active, TrayIconState.Paused, true)]
    [InlineData(TrayIconState.Degraded, TrayIconState.Error, true)]
    [InlineData(TrayIconState.Paused, TrayIconState.Active, false)]
    public void NotificationsOnlyFireForNewSignificantStates(TrayIconState previous, TrayIconState current, bool expected)
    {
        Assert.Equal(expected, TrayNotificationPolicy.ShouldNotify(previous, current));
    }
}
