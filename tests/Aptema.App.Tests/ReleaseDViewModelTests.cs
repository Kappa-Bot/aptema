using Aptema.App.Presentation;
using Aptema.App.ViewModels;
using Aptema.Core;

namespace Aptema.App.Tests;

public sealed class ReleaseDViewModelTests
{
    [Fact]
    public void OverlayBoundsRejectsInvalidRectangleAndUsesExactPhysicalPixels()
    {
        Assert.False(OverlayBoundsPolicy.TryGetPhysicalBounds(default, out _));
        Assert.True(OverlayBoundsPolicy.TryGetPhysicalBounds(new DisplayBounds(-1920, 0, 1920, 1080), out var bounds));
        Assert.Equal(new PixelRectangle(-1920, 0, 1920, 1080), bounds);
    }

    [Fact]
    public void OnboardingNavigatesSixStepsAndRequiresPrivacyAcknowledgement()
    {
        var model = new OnboardingViewModel(UserSettings.Default, [], false);

        Assert.Equal(OnboardingStep.Welcome, model.CurrentStep);
        Assert.False(model.CanMoveNext);
        model.PrivacyAccepted = true;
        Assert.True(model.CanMoveNext);

        for (var index = 0; index < 5; index++) model.MoveNext();

        Assert.Equal(OnboardingStep.Finish, model.CurrentStep);
        Assert.True(model.CanMoveBack);
    }

    [Fact]
    public void OnboardingDoesNotRequestDisplayTestUntilExplicitAction()
    {
        var model = new OnboardingViewModel(UserSettings.Default, [Display()], false);
        var requests = 0;
        model.DisplayTestRequested += (_, _) => requests++;

        model.PrivacyAccepted = true;
        model.MoveNext();
        model.MoveNext();
        Assert.Equal(0, requests);

        model.TestSelectedDisplayCommand.Execute(null);
        Assert.Equal(1, requests);
    }

    [Fact]
    public void OnboardingScheduleRequiresValidTimes()
    {
        var model = new OnboardingViewModel(UserSettings.Default, [], false) { PrivacyAccepted = true };
        model.MoveNext(); model.MoveNext(); model.MoveNext(); model.MoveNext();
        model.WakeTime = "not-a-time";

        Assert.Equal(OnboardingStep.Schedule, model.CurrentStep);
        Assert.False(model.CanMoveNext);
        model.WakeTime = "07:30";
        model.SleepTime = "23:15";
        Assert.True(model.CanMoveNext);
    }

    [Fact]
    public void DisplayDraftSaveProducesSettingsAndCancelLeavesOriginalUntouched()
    {
        var original = UserSettings.Default with
        {
            DisplayConfigurations = [new DisplayConfiguration("display:one", ["device:\\\\.\\DISPLAY1"], true, 0, 25, 90, true)]
        };
        var draft = new DisplaySettingsViewModel(original, [Display()]);
        var card = Assert.Single(draft.Displays);
        card.IsEnabled = false;
        card.BrightnessOffsetPercent = -7;
        card.MinimumBrightnessPercent = 30;
        card.MaximumBrightnessPercent = 80;

        var saved = draft.ToSettings(original);

        Assert.True(original.DisplayConfigurations[0].IsEnabled);
        Assert.False(saved.DisplayConfigurations[0].IsEnabled);
        Assert.Equal(-7, saved.DisplayConfigurations[0].BrightnessOffsetPercent);
        Assert.Equal(30, saved.DisplayConfigurations[0].MinimumBrightnessPercent);
        Assert.Equal(80, saved.DisplayConfigurations[0].MaximumBrightnessPercent);
    }

    private static MonitorModel Display() => new("display:one", "Display 1", true, true, 15, 100, 0, 11,
        new DisplayBounds(0, 0, 1920, 1080), new DisplayBounds(0, 0, 1920, 1040), true,
        ["device:\\\\.\\DISPLAY1"], "Dell U2723QE");
}
