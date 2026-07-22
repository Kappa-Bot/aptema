using LightPilot.App.Services;

namespace LightPilot.App.Tests;

public sealed class TrayCommandRouterTests
{
    [Theory]
    [InlineData(TrayMenuCommandKey.TooBright, "TooBright")]
    [InlineData(TrayMenuCommandKey.TooDim, "TooDim")]
    [InlineData(TrayMenuCommandKey.Warmer, "Warmer")]
    [InlineData(TrayMenuCommandKey.Cooler, "Cooler")]
    [InlineData(TrayMenuCommandKey.Perfect, "Perfect")]
    [InlineData(TrayMenuCommandKey.Settings, "Settings")]
    public void RoutesEveryDailyActionToSharedTarget(TrayMenuCommandKey command, string expected)
    {
        var target = new RecordingTarget();
        var router = new TrayCommandRouter(target);

        router.Execute(command);

        Assert.Equal(expected, target.LastAction);
    }

    private sealed class RecordingTarget : ITrayCommandTarget
    {
        public string? LastAction { get; private set; }
        public void ToggleAuto() => LastAction = "ToggleAuto";
        public void PauseThirtyMinutes() => LastAction = "Pause";
        public void PauseUntilTomorrow() => LastAction = "PauseTomorrow";
        public void TooBright() => LastAction = "TooBright";
        public void TooDim() => LastAction = "TooDim";
        public void Warmer() => LastAction = "Warmer";
        public void Cooler() => LastAction = "Cooler";
        public void Perfect() => LastAction = "Perfect";
        public void Open() => LastAction = "Open";
        public void Settings() => LastAction = "Settings";
        public void Exit() => LastAction = "Exit";
    }
}
