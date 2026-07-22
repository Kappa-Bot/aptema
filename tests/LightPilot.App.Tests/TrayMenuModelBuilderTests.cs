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
                TrayMenuCommandKey.PauseOrResume,
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

    [Fact]
    public void PausedTrayOffersResumeInsteadOfAnotherPause()
    {
        var items = TrayMenuModelBuilder.Build(new TrayMenuState(true, true, "Paused", "Pause", "Soon"));

        var action = Assert.Single(items, item => item.CommandKey == TrayMenuCommandKey.PauseOrResume);
        Assert.Equal("Resume", action.Text);
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
        var now = new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
        var oldIdentity = new TrayNotificationIdentity(previous, previous.ToString());
        var newIdentity = new TrayNotificationIdentity(current, current.ToString());
        Assert.Equal(expected, TrayNotificationPolicy.ShouldNotify(oldIdentity, newIdentity, now.AddMinutes(-3), now));
    }

    [Fact]
    public void NotificationCooldownDeduplicatesSameHealthIdentity()
    {
        var now = new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
        var identity = new TrayNotificationIdentity(TrayIconState.Degraded, "display-1:ddc");

        Assert.False(TrayNotificationPolicy.ShouldNotify(identity, identity, now.AddMinutes(-5), now));
        Assert.False(TrayNotificationPolicy.ShouldNotify(
            new TrayNotificationIdentity(TrayIconState.Degraded, "display-1:old"),
            identity,
            now.AddSeconds(-30),
            now));
    }

    [Fact]
    public void RecoveredIncidentCanNotifyAgainAfterCooldownWithoutOscillationSpam()
    {
        var tracker = new TrayNotificationTracker();
        var started = new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
        var degraded = new TrayNotificationIdentity(TrayIconState.Degraded, "display-1:ddc");
        var active = new TrayNotificationIdentity(TrayIconState.Active, string.Empty);

        Assert.True(tracker.ShouldNotify(degraded, started));
        Assert.False(tracker.ShouldNotify(active, started.AddSeconds(20)));
        Assert.False(tracker.ShouldNotify(degraded, started.AddSeconds(40)));
        Assert.False(tracker.ShouldNotify(active, started.AddMinutes(2.5)));
        Assert.True(tracker.ShouldNotify(degraded, started.AddMinutes(3)));
    }

    [Fact]
    public void ShortcutConflictAddsHumanHelpAction()
    {
        var items = TrayMenuModelBuilder.Build(new TrayMenuState(true, false, "Comfortable", "Day", "Soon", ShortcutAvailable: false));

        Assert.Contains(items, item => item.CommandKey == TrayMenuCommandKey.ShortcutHelp && item.Text.Contains("shortcut", StringComparison.OrdinalIgnoreCase));
    }
}
