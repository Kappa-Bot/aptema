namespace LightPilot.App.Services;

public enum TrayMenuCommandKey
{
    ToggleAuto,
    PauseOrResume,
    PauseUntilTomorrow,
    TooBright,
    TooDim,
    Warmer,
    Cooler,
    Perfect,
    Open,
    Settings,
    ShortcutHelp,
    Exit
}

public sealed record TrayMenuState(
    bool AutoEnabled,
    bool IsPaused,
    string ComfortState,
    string Mode,
    string NextAdaptationText,
    bool ShortcutAvailable = true);

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
            new(TrayMenuCommandKey.PauseOrResume, state.IsPaused ? "Resume" : "Pause 30 min"),
            new(TrayMenuCommandKey.PauseUntilTomorrow, "Pause until tomorrow"),
            Separator(),
            new(TrayMenuCommandKey.TooBright, "Too bright"),
            new(TrayMenuCommandKey.TooDim, "Too dim"),
            new(TrayMenuCommandKey.Warmer, "Warmer"),
            new(TrayMenuCommandKey.Cooler, "Cooler"),
            new(TrayMenuCommandKey.Perfect, "Perfect"),
            Separator(),
            ..(!state.ShortcutAvailable
                ? new[] { new TrayMenuItemModel(TrayMenuCommandKey.ShortcutHelp, "Quick Adjust shortcut unavailable") }
                : Array.Empty<TrayMenuItemModel>()),
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

public sealed record TrayNotificationIdentity(TrayIconState State, string HealthIdentity);

public static class TrayNotificationPolicy
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(2);

    public static bool ShouldNotify(
        TrayNotificationIdentity? previous,
        TrayNotificationIdentity current,
        DateTimeOffset? lastNotifiedAt,
        DateTimeOffset now) =>
        current.State is TrayIconState.Paused or TrayIconState.Degraded or TrayIconState.Error
        && previous != current
        && (lastNotifiedAt is null || now - lastNotifiedAt >= Cooldown);
}
