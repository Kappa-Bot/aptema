namespace Aptema.Core;

public static class ComfortPreferenceAdvisor
{
    private const int FeedbackStep = 5;

    public static UserSettings Apply(UserSettings settings, ComfortFeedback feedback)
    {
        var delta = feedback switch
        {
            ComfortFeedback.TooBright => FeedbackStep,
            ComfortFeedback.TooDim => -FeedbackStep,
            _ => 0
        };

        return settings with
        {
            AutoEnabled = true,
            ComfortIntensity = Math.Clamp(settings.ComfortIntensity + delta, 0, 100)
        };
    }

    public static UserSettings Apply(UserSettings settings, ComfortFeedback feedback, PreferenceLearningContext context, DateTimeOffset now)
    {
        var updated = Apply(settings, feedback);
        if (!settings.EnablePreferenceLearning)
        {
            return updated;
        }

        return updated with
        {
            PreferenceLearning = PreferenceLearningService.RecordFeedback(settings.PreferenceLearning, context, feedback, now)
        };
    }
}
