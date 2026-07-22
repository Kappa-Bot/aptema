using Aptema.App.Presentation;

namespace Aptema.App.Tests;

public sealed class ShellNavigationTests
{
    [Fact]
    public void BuildsCompleteAptemaNavigationInProductOrder()
    {
        var items = ShellNavigation.Build();

        Assert.Equal(
            [ShellSurface.Home, ShellSurface.Displays, ShellSurface.Applications, ShellSurface.Profiles,
             ShellSurface.Automation, ShellSurface.Learning, ShellSurface.System],
            items.Select(item => item.Surface));
        Assert.All(items, item => Assert.False(string.IsNullOrWhiteSpace(item.AccessibleName)));
    }

    [Fact]
    public void SelectionReturnsFunctionalSurfacePresentation()
    {
        var model = new ShellPresentationModel();

        model.Select(ShellSurface.Displays);

        Assert.Equal(ShellSurface.Displays, model.SelectedSurface);
        Assert.Equal("Displays", model.Current.Title);
        Assert.False(string.IsNullOrWhiteSpace(model.Current.Summary));
    }
}
