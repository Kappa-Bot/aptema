namespace Aptema.App.Presentation;

public enum ShellSurface
{
    Home,
    Displays,
    Applications,
    Profiles,
    Automation,
    Learning,
    System
}

public sealed record ShellNavigationItem(
    ShellSurface Surface,
    string Label,
    string AccessibleName,
    string Title,
    string Summary);

public static class ShellNavigation
{
    private static readonly IReadOnlyList<ShellNavigationItem> Items =
    [
        new(ShellSurface.Home, "Home", "Open comfort home", "Comfort", "Your screens adapt quietly as your day changes."),
        new(ShellSurface.Displays, "Displays", "Open display preferences", "Displays", "Choose which screens Aptema protects."),
        new(ShellSurface.Applications, "Applications", "Open application preferences", "Applications", "Personalize comfort for the apps you use."),
        new(ShellSurface.Profiles, "Profiles", "Open comfort profiles", "Profiles", "Choose a starting point for different kinds of work."),
        new(ShellSurface.Automation, "Automation", "Open automation", "Automation", "Control when Aptema adapts and when it stays still."),
        new(ShellSurface.Learning, "Learning", "Open learned preferences", "Learning", "Review the local preferences Aptema has learned."),
        new(ShellSurface.System, "System", "Open system settings", "System", "Privacy, startup, diagnostics and app preferences.")
    ];

    public static IReadOnlyList<ShellNavigationItem> Build() => Items;

    public static ShellNavigationItem Get(ShellSurface surface) =>
        Items.First(item => item.Surface == surface);
}

public sealed class ShellPresentationModel
{
    public ShellSurface SelectedSurface { get; private set; } = ShellSurface.Home;

    public ShellNavigationItem Current => ShellNavigation.Get(SelectedSurface);

    public void Select(ShellSurface surface) => SelectedSurface = surface;
}
