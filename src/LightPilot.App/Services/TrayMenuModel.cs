namespace LightPilot.App.Services;

public enum TrayMenuCommandKey
{
    ToggleAuto,
    PauseThirtyMinutes,
    PauseUntilTomorrow,
    TooBright,
    TooDim,
    Warmer,
    Cooler,
    Perfect,
    Open,
    Settings,
    Exit
}

public sealed record TrayMenuState(
    bool AutoEnabled,
    bool IsPaused,
    string ComfortState,
    string Mode,
    string NextAdaptationText);

public sealed record TrayMenuItemModel(
    TrayMenuCommandKey CommandKey,
    string Text,
    bool IsChecked = false,
    bool IsEnabled = true,
    bool IsSeparator = false);

public static class TrayMenuModelBuilder
{
    private const int NotifyIconTextLimit = 63;

    public static IReadOnlyList<TrayMenuItemModel> Build(TrayMenuState state)
    {
        return
        [
            new(TrayMenuCommandKey.ToggleAuto, state.AutoEnabled ? "Auto on" : "Auto off", state.AutoEnabled),
            Separator(),
            new(TrayMenuCommandKey.PauseThirtyMinutes, "Pause 30 min"),
            new(TrayMenuCommandKey.PauseUntilTomorrow, "Pause until tomorrow"),
            Separator(),
            new(TrayMenuCommandKey.TooBright, "Too bright"),
            new(TrayMenuCommandKey.TooDim, "Too dim"),
            new(TrayMenuCommandKey.Warmer, "Warmer"),
            new(TrayMenuCommandKey.Cooler, "Cooler"),
            new(TrayMenuCommandKey.Perfect, "Perfect"),
            Separator(),
            new(TrayMenuCommandKey.Open, "Open Aptema"),
            new(TrayMenuCommandKey.Settings, "Settings"),
            Separator(),
            new(TrayMenuCommandKey.Exit, "Exit")
        ];
    }

    public static string BuildTooltip(TrayMenuState state)
    {
        var core = state.AutoEnabled && !state.IsPaused
            ? $"{state.ComfortState} - {state.Mode} - {state.NextAdaptationText}"
            : $"{state.ComfortState} - {state.Mode}";

        return Truncate(core);
    }

    private static TrayMenuItemModel Separator()
    {
        return new TrayMenuItemModel(TrayMenuCommandKey.Open, "", IsSeparator: true, IsEnabled: false);
    }

    private static string Truncate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Aptema";
        }

        var text = $"Aptema - {value.Trim()}";
        if (text.Length <= NotifyIconTextLimit)
        {
            return text;
        }

        return text[..Math.Max(0, NotifyIconTextLimit - 3)] + "...";
    }
}

public enum TrayIconState
{
    Active,
    Paused,
    Degraded,
    Error
}

public static class TrayPresentation
{
    public static TrayIconState ResolveIconState(bool isPaused, bool isDegraded, bool hasError)
    {
        if (hasError)
        {
            return TrayIconState.Error;
        }

        if (isDegraded)
        {
            return TrayIconState.Degraded;
        }

        return isPaused ? TrayIconState.Paused : TrayIconState.Active;
    }
}

public static class TrayNotificationPolicy
{
    public static bool ShouldNotify(TrayIconState previous, TrayIconState current) =>
        previous != current && current is TrayIconState.Paused or TrayIconState.Degraded or TrayIconState.Error;
}
