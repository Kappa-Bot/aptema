using Aptema.Core;

namespace Aptema.Core.Tests;

public sealed class PersonalizationPolicyTests
{
    [Fact]
    public void ResolvesApplicationRuleCaseInsensitivelyByPriorityThenStableId()
    {
        var settings = UserSettings.Default with
        {
            ApplicationRules =
            [
                new("z-rule", "Fallback", true, 5, "chrome.exe", AppCategory.Browser, ComfortProfileId.Reading, null, -2, true),
                new("a-rule", "Night browser", true, 10, "CHROME.EXE", AppCategory.OfficeReading, ComfortProfileId.Evening, null, -4, true),
                new("b-rule", "Tie loses", true, 10, "chrome.exe", AppCategory.Unknown, null, null, 0, false)
            ]
        };

        var result = PersonalizationPolicy.Resolve(Context("Chrome.exe"), DayPhase.Night, LuminanceClassification.Bright, settings);

        Assert.Equal("a-rule", result.ApplicationRuleId);
        Assert.Equal(AppCategory.OfficeReading, result.AppCategory);
        Assert.Equal(ComfortProfileId.Evening, result.Profile);
        Assert.Equal(-4, result.IntensityOffset);
        Assert.True(result.ProtectFullscreen);
        Assert.Equal("Night browser", result.ResponsibleRule);
    }

    [Fact]
    public void ResolvesCustomProfileWithoutChangingBuiltInProfiles()
    {
        var custom = new CustomComfortProfile("profile-soft-code", "Soft coding", 66, 52, 38, 6000, 4700, 3600, 110, true);
        var settings = UserSettings.Default with
        {
            CustomProfiles = [custom],
            ApplicationRules = [new("code", "Coding", true, 20, "code.exe", AppCategory.Development, null, custom.Id, 0, true)]
        };

        var result = PersonalizationPolicy.Resolve(Context("code.exe"), DayPhase.Night, LuminanceClassification.Dark, settings);

        Assert.Equal(custom, result.CustomProfile);
        Assert.True(result.ProtectFullscreen);
        Assert.Equal("Soft coding", result.ProfileName);
        Assert.Equal(ComfortProfileId.Development, result.Profile);
    }

    [Fact]
    public void AutomationRulesMatchAllSpecifiedConditionsAndClampCombinedOffsets()
    {
        var settings = UserSettings.Default with
        {
            AutomationRules =
            [
                new("night-reading", "Night reading", true, 20, DayPhase.Night, AppCategory.Browser, false, LuminanceClassification.MostlyWhite, -9, -350),
                new("night-global", "Night softening", true, 5, DayPhase.Night, null, null, null, -8, -300),
                new("wrong-fullscreen", "Wrong", true, 99, DayPhase.Night, AppCategory.Browser, true, null, 12, 480)
            ]
        };

        var result = PersonalizationPolicy.Resolve(Context("chrome.exe", AppCategory.Browser), DayPhase.Night, LuminanceClassification.MostlyWhite, settings);

        Assert.Equal(-12, result.BrightnessOffsetPercent);
        Assert.Equal(-480, result.WarmthOffsetKelvin);
        Assert.Equal(["night-reading", "night-global"], result.AutomationRuleIds);
        Assert.DoesNotContain("wrong-fullscreen", result.AutomationRuleIds);
    }

    [Fact]
    public void DisabledAndNonMatchingRulesHaveNoEffect()
    {
        var settings = UserSettings.Default with
        {
            ApplicationRules = [new("off", "Disabled", false, 100, "code.exe", AppCategory.Gaming, ComfortProfileId.Gaming, null, -12, true)],
            AutomationRules = [new("day", "Day only", true, 1, DayPhase.Day, null, null, null, -4, -100)]
        };

        var result = PersonalizationPolicy.Resolve(Context("code.exe", AppCategory.Development), DayPhase.Night, LuminanceClassification.Dark, settings);

        Assert.Null(result.ApplicationRuleId);
        Assert.Empty(result.AutomationRuleIds);
        Assert.Equal(AppCategory.Development, result.AppCategory);
        Assert.Equal(0, result.BrightnessOffsetPercent);
    }

    private static AppContextModel Context(string process, AppCategory category = AppCategory.Unknown, bool fullscreen = false) =>
        new(process, category, fullscreen);
}
