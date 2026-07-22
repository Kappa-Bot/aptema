namespace Aptema.Core;

public sealed record PersonalizationResolution(
    AppCategory AppCategory,
    ComfortProfileId Profile,
    string ProfileName,
    CustomComfortProfile? CustomProfile,
    int IntensityOffset,
    bool ProtectFullscreen,
    int BrightnessOffsetPercent,
    int WarmthOffsetKelvin,
    string? ApplicationRuleId,
    IReadOnlyList<string> AutomationRuleIds,
    string? ResponsibleRule);

public static class PersonalizationPolicy
{
    public static PersonalizationResolution Resolve(
        AppContextModel context,
        DayPhase dayPhase,
        LuminanceClassification luminance,
        UserSettings settings)
    {
        var appRule = (settings.ApplicationRules ?? Array.Empty<ApplicationComfortRule>())
            .Where(rule => rule.IsEnabled && ProcessMatches(rule.ProcessName, context.ProcessName))
            .OrderByDescending(rule => rule.Priority)
            .ThenBy(rule => rule.Id, StringComparer.Ordinal)
            .FirstOrDefault();

        var category = appRule?.Category ?? context.Category;
        var fallbackProfile = ProfileFor(category, dayPhase);
        var custom = string.IsNullOrWhiteSpace(appRule?.CustomProfileId)
            ? null
            : (settings.CustomProfiles ?? Array.Empty<CustomComfortProfile>()).FirstOrDefault(profile =>
                string.Equals(profile.Id, appRule.CustomProfileId, StringComparison.OrdinalIgnoreCase));
        var profile = appRule?.Profile ?? fallbackProfile;

        var automation = (settings.AutomationRules ?? Array.Empty<ComfortAutomationRule>())
            .Where(rule => Matches(rule, dayPhase, category, context.IsFullscreen, luminance))
            .OrderByDescending(rule => rule.Priority)
            .ThenBy(rule => rule.Id, StringComparer.Ordinal)
            .ToArray();

        return new PersonalizationResolution(
            category,
            profile,
            custom?.Name ?? profile.ToString(),
            custom,
            Math.Clamp(appRule?.IntensityOffset ?? 0, -20, 20),
            (appRule?.ProtectFullscreen ?? false) || (custom?.ProtectFullscreen ?? false),
            Math.Clamp(automation.Sum(rule => rule.BrightnessOffsetPercent), -12, 12),
            Math.Clamp(automation.Sum(rule => rule.WarmthOffsetKelvin), -480, 480),
            appRule?.Id,
            automation.Select(rule => rule.Id).ToArray(),
            appRule?.Name ?? automation.FirstOrDefault()?.Name);
    }

    private static bool Matches(
        ComfortAutomationRule rule,
        DayPhase phase,
        AppCategory category,
        bool fullscreen,
        LuminanceClassification luminance) =>
        rule.IsEnabled &&
        (rule.DayPhase is null || rule.DayPhase == phase) &&
        (rule.AppCategory is null || rule.AppCategory == category) &&
        (rule.IsFullscreen is null || rule.IsFullscreen == fullscreen) &&
        (rule.Luminance is null || rule.Luminance == luminance);

    private static bool ProcessMatches(string expected, string? actual)
    {
        static string Normalize(string value) => Path.GetFileName(value.Trim());
        return !string.IsNullOrWhiteSpace(expected) &&
               !string.IsNullOrWhiteSpace(actual) &&
               string.Equals(Normalize(expected), Normalize(actual), StringComparison.OrdinalIgnoreCase);
    }

    private static ComfortProfileId ProfileFor(AppCategory category, DayPhase phase) => category switch
    {
        AppCategory.Gaming => ComfortProfileId.Gaming,
        AppCategory.VideoMedia => ComfortProfileId.Video,
        AppCategory.Development => ComfortProfileId.Development,
        AppCategory.OfficeReading or AppCategory.EmailCommunication => ComfortProfileId.Reading,
        _ => phase switch
        {
            DayPhase.Day => ComfortProfileId.Day,
            DayPhase.Evening => ComfortProfileId.Evening,
            _ => ComfortProfileId.Night
        }
    };
}
