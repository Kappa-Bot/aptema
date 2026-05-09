namespace LightPilot.Core;

public static class FatigueModel
{
    private static readonly TimeSpan FatigueStartsAt = TimeSpan.FromMinutes(75);
    private static readonly TimeSpan FullRampDuration = TimeSpan.FromMinutes(45);

    public static PreferenceAdjustment Calculate(AdaptiveSnapshot snapshot, UserSettings settings)
    {
        if (snapshot.ScreenTimeSessionLength < FatigueStartsAt ||
            snapshot.AppContext.Category is AppCategory.Gaming or AppCategory.VideoMedia)
        {
            return PreferenceAdjustment.None;
        }

        var intensityFactor = Math.Clamp(settings.ComfortIntensity, 0, 100) / 100d;
        var excess = Math.Min(1, (snapshot.ScreenTimeSessionLength - FatigueStartsAt).TotalMinutes / FullRampDuration.TotalMinutes);
        var brightness = -(int)Math.Round(4 * excess * intensityFactor);
        var warmth = -(int)Math.Round(180 * excess * intensityFactor);

        return new PreferenceAdjustment(brightness, warmth, 0.65, IsLearned: false);
    }

    public static PreferenceAdjustment CalculateLearned(AdaptiveSnapshot snapshot, PreferenceAdjustment learned)
    {
        if (snapshot.ScreenTimeSessionLength < FatigueStartsAt ||
            snapshot.AppContext.Category is AppCategory.Gaming or AppCategory.VideoMedia ||
            !learned.IsLearned)
        {
            return PreferenceAdjustment.None;
        }

        var brightness = Math.Clamp(Math.Min(learned.BrightnessOffsetPercent, 0), -2, 0);
        var warmth = Math.Clamp(Math.Min(learned.WarmthOffsetKelvin, 0), -90, 0);
        return new PreferenceAdjustment(brightness, warmth, learned.Confidence, brightness != 0 || warmth != 0);
    }
}
