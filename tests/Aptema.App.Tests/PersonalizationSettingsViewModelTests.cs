using Aptema.App.ViewModels;
using Aptema.Core;

namespace Aptema.App.Tests;

public sealed class PersonalizationSettingsViewModelTests
{
    [Fact]
    public void ClassifyingCurrentApplicationUpsertsProcessOnlyRule()
    {
        var model = new PersonalizationSettingsViewModel(UserSettings.Default, "C:\\Apps\\Code.exe", AppCategory.Unknown);
        model.SelectedApplicationCategory = AppCategory.Development;

        model.SaveCurrentApplicationRule();
        var settings = model.ToSettings(UserSettings.Default);

        var rule = Assert.Single(settings.ApplicationRules);
        Assert.Equal("Code.exe", rule.ProcessName);
        Assert.Equal(AppCategory.Development, rule.Category);
        Assert.DoesNotContain("C:\\Apps", rule.ProcessName);
    }

    [Fact]
    public void CreatingAndRemovingCustomProfileDoesNotModifyBuiltIns()
    {
        var model = new PersonalizationSettingsViewModel(UserSettings.Default, null, AppCategory.Unknown)
        {
            NewProfileName = "Soft focus"
        };

        model.CreateProfile();
        var profile = Assert.Single(model.CustomProfiles);
        Assert.InRange(profile.NightBrightness, 15, 100);
        Assert.InRange(profile.NightKelvin, 2800, 6500);

        model.RemoveProfile(profile.Id);
        Assert.Empty(model.CustomProfiles);
        Assert.Equal(10, new ProfileManager().Profiles.Count);
    }

    [Fact]
    public void AutomationEditorClampsOffsetsAndUsesStablePriority()
    {
        var model = new PersonalizationSettingsViewModel(UserSettings.Default, null, AppCategory.Unknown)
        {
            NewAutomationName = "Late softening",
            NewAutomationPhase = DayPhase.Night,
            NewAutomationBrightnessOffset = -40,
            NewAutomationWarmthOffset = -900
        };

        model.CreateAutomationRule();
        var rule = Assert.Single(model.AutomationRules);

        Assert.Equal(-12, rule.BrightnessOffsetPercent);
        Assert.Equal(-480, rule.WarmthOffsetKelvin);
        Assert.Equal(DayPhase.Night, rule.DayPhase);
    }

    [Fact]
    public void ResetLearningIsStagedUntilSettingsAreSaved()
    {
        var aggregate = new PreferenceCorrectionAggregate { Key = "one", Samples = 4 };
        var current = UserSettings.Default with { PreferenceLearning = new PreferenceLearningModel { Aggregates = [aggregate] } };
        var model = new PersonalizationSettingsViewModel(current, null, AppCategory.Unknown);

        model.ResetLearning();

        Assert.Single(current.PreferenceLearning.Aggregates);
        Assert.Empty(model.ToSettings(current).PreferenceLearning.Aggregates);
    }
}
