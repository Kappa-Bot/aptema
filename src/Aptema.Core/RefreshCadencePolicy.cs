namespace Aptema.Core;

public static class RefreshCadencePolicy
{
    public static TimeSpan GetInterval(UserSettings settings, bool isPaused, bool isContentAnalysisEnabled)
    {
        if (isPaused || !settings.AutoEnabled)
        {
            return TimeSpan.FromSeconds(30);
        }

        return isContentAnalysisEnabled || settings.EnableContentBrightnessAnalysis
            ? TimeSpan.FromSeconds(5)
            : TimeSpan.FromSeconds(10);
    }
}
