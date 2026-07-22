namespace Aptema.Core;

public static class PowerAwareSettingsPolicy
{
    public static UserSettings Apply(UserSettings settings, bool isOnBattery)
    {
        if (!isOnBattery || !settings.ReduceWorkOnBattery)
        {
            return settings;
        }

        return settings with
        {
            EnableContentBrightnessAnalysis = false,
            TransitionSpeed = settings.TransitionSpeed < TimeSpan.FromSeconds(120)
                ? TimeSpan.FromSeconds(120)
                : settings.TransitionSpeed
        };
    }
}
