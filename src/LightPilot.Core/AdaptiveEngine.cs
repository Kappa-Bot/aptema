namespace LightPilot.Core;

public sealed class AdaptiveEngine
{
    private static readonly TimeSpan AutomaticCooldown = TimeSpan.FromSeconds(30);
    private readonly ProfileManager _profiles = new();

    public ComfortDecision Evaluate(AdaptiveSnapshot snapshot, AdaptiveEngineState state, UserSettings settings)
    {
        if (!settings.AutoEnabled)
        {
            return Decision(ComfortProfileId.Paused, snapshot.CurrentBrightness, snapshot.CurrentColorTemperatureKelvin, 0, false, "Auto is paused", "PolicyDisabled", settings, DecisionSource.Paused, confidence: 1);
        }

        if (snapshot.ManualOverrideUntil is { } manualUntil && manualUntil > snapshot.Now)
        {
            return Decision(ComfortProfileId.Manual, snapshot.CurrentBrightness, snapshot.CurrentColorTemperatureKelvin, 0, false, "Manual adjustment cooldown", "ManualOverrideActive", settings, DecisionSource.Manual, confidence: 1);
        }

        var profile = SelectProfile(snapshot);

        if (settings.GamingVideoProtection && snapshot.AppContext.IsPresentation)
        {
            return Decision(ComfortProfileId.Video, snapshot.CurrentBrightness, snapshot.CurrentColorTemperatureKelvin, 0, false, "Presentation protected", "SuppressedForPresentation", settings, DecisionSource.Protected, confidence: 1);
        }

        if (settings.GamingVideoProtection && snapshot.AppContext.Category == AppCategory.Gaming && snapshot.AppContext.IsFullscreen)
        {
            return Decision(ComfortProfileId.Gaming, snapshot.CurrentBrightness, snapshot.CurrentColorTemperatureKelvin, 0, false, "Gaming fullscreen protection", "SuppressedForGame", settings, DecisionSource.Protected, confidence: 1);
        }

        if (settings.GamingVideoProtection && snapshot.AppContext.Category == AppCategory.VideoMedia && snapshot.AppContext.IsFullscreen)
        {
            return Decision(ComfortProfileId.Video, snapshot.CurrentBrightness, snapshot.CurrentColorTemperatureKelvin, 0, false, "Video playback protected", "SuppressedForVideoPlayback", settings, DecisionSource.Protected, confidence: 1);
        }

        var raw = ComputeTarget(snapshot, profile, settings);
        var targetBrightness = raw.BrightnessPercent;
        var targetKelvin = raw.ColorTemperatureKelvin;
        var reason = BuildReason(snapshot, profile);
        var reasonCode = profile.ToString();

        if (state.LastAppliedAt is { } lastApplied && snapshot.Now - lastApplied < AutomaticCooldown)
        {
            return new ComfortDecision(profile, targetBrightness, targetKelvin, raw.OverlayOpacity, settings.TransitionSpeed, false, "Cooling down before next adjustment", new[] { "CooldownActive" }, raw.Confidence, raw.Source, raw.IsLearned);
        }

        if (state.LastDecision is { } last && Math.Abs(targetBrightness - last.BrightnessPercent) < 2 && Math.Abs(targetKelvin - last.ColorTemperatureKelvin) < 100)
        {
            return new ComfortDecision(profile, targetBrightness, targetKelvin, raw.OverlayOpacity, settings.TransitionSpeed, false, "No visible change needed", new[] { "NoChangeWithinHysteresis" }, raw.Confidence, raw.Source, raw.IsLearned);
        }

        return new ComfortDecision(profile, targetBrightness, targetKelvin, raw.OverlayOpacity, settings.TransitionSpeed, true, reason, new[] { reasonCode }, raw.Confidence, raw.Source, raw.IsLearned);
    }

    public ComfortDecision EvaluateManualFeedback(AdaptiveSnapshot snapshot, UserSettings settings, ComfortFeedback feedback)
    {
        var profile = snapshot.AppContext.Category switch
        {
            AppCategory.Gaming => ComfortProfileId.Gaming,
            AppCategory.VideoMedia => ComfortProfileId.Video,
            _ => ComfortProfileId.Manual
        };

        var brightnessDelta = feedback switch
        {
            ComfortFeedback.TooBright => -6,
            ComfortFeedback.TooDim => 6,
            _ => 0
        };
        var kelvinDelta = feedback switch
        {
            ComfortFeedback.TooBright => -300,
            ComfortFeedback.TooDim => 300,
            ComfortFeedback.TooWarm => 300,
            ComfortFeedback.TooCold => -300,
            ComfortFeedback.Perfect => 0,
            _ => 0
        };

        var protectedFullscreen = settings.GamingVideoProtection &&
            (snapshot.AppContext.IsPresentation ||
             (snapshot.AppContext.IsFullscreen && snapshot.AppContext.Category is AppCategory.Gaming or AppCategory.VideoMedia));
        var brightness = protectedFullscreen
            ? snapshot.CurrentBrightness
            : ClampBrightness(snapshot.CurrentBrightness + brightnessDelta, snapshot.Monitor, settings);
        var kelvin = Math.Clamp(snapshot.CurrentColorTemperatureKelvin + kelvinDelta, 2800, 6500);
        var overlay = CalculateOverlay(kelvin);
        var reason = feedback == ComfortFeedback.Perfect ? "Comfort marked as right" : "Manual comfort correction";

        return new ComfortDecision(profile, brightness, kelvin, overlay, TimeSpan.FromSeconds(18), true, reason, new[] { "ManualFeedback" }, 1, DecisionSource.Manual);
    }

    private LightTarget ComputeTarget(AdaptiveSnapshot snapshot, ComfortProfileId profileId, UserSettings settings)
    {
        var profile = _profiles.Get(profileId);
        var phase = DayPhasePolicy.GetPhase(snapshot.Now);
        var brightness = phase switch
        {
            DayPhase.Day => profile.DayBrightness,
            DayPhase.Evening => profile.EveningBrightness,
            DayPhase.Night => profile.NightBrightness,
            _ => profile.DayBrightness
        };
        var kelvin = phase switch
        {
            DayPhase.Day => profile.DayKelvin,
            DayPhase.Evening => profile.EveningKelvin,
            DayPhase.Night => profile.NightKelvin,
            _ => profile.DayKelvin
        };

        var intensityFactor = Math.Clamp(settings.ComfortIntensity, 0, 100) / 100d;
        if (snapshot.Content.IsEnabled && snapshot.AppContext.Category is AppCategory.Browser or AppCategory.EmailCommunication or AppCategory.OfficeReading)
        {
            if (snapshot.Content.Classification == LuminanceClassification.MostlyWhite)
            {
                brightness -= (int)Math.Round(6 * intensityFactor);
                kelvin -= (int)Math.Round(220 * intensityFactor);
            }
            else if (snapshot.Content.Classification == LuminanceClassification.Bright)
            {
                brightness -= (int)Math.Round(3 * intensityFactor);
                kelvin -= (int)Math.Round(120 * intensityFactor);
            }
            else if (snapshot.Content.Classification == LuminanceClassification.HighContrast)
            {
                kelvin -= (int)Math.Round(80 * intensityFactor);
            }
        }

        var fatigue = FatigueModel.Calculate(snapshot, settings);
        brightness += fatigue.BrightnessOffsetPercent;
        kelvin += fatigue.WarmthOffsetKelvin;

        if (snapshot.Now.TimeOfDay < TimeSpan.FromHours(3))
        {
            brightness -= (int)Math.Round(2 * intensityFactor);
            kelvin -= (int)Math.Round(140 * intensityFactor);
        }

        var learned = settings.EnablePreferenceLearning
            ? PreferenceLearningService.GetAdjustment(settings.PreferenceLearning, PreferenceLearningContext.FromSnapshot(snapshot))
            : PreferenceAdjustment.None;
        var learnedFatigue = FatigueModel.CalculateLearned(snapshot, learned);
        brightness += learned.BrightnessOffsetPercent + learnedFatigue.BrightnessOffsetPercent;
        kelvin += learned.WarmthOffsetKelvin + learnedFatigue.WarmthOffsetKelvin;

        brightness += snapshot.Monitor.BrightnessOffsetPercent;
        brightness = LimitStep(snapshot.CurrentBrightness, brightness, maxStep: 3);
        brightness = ClampBrightness(brightness, snapshot.Monitor, settings);
        kelvin = Math.Clamp(kelvin, 2800, 6500);
        kelvin = LimitStep(snapshot.CurrentColorTemperatureKelvin, kelvin, maxStep: 200);
        var overlay = CalculateOverlay(kelvin);

        return new LightTarget(brightness, kelvin, overlay, learned.Confidence, learned.IsLearned ? DecisionSource.Learned : DecisionSource.Default, learned.IsLearned);
    }

    private static ComfortProfileId SelectProfile(AdaptiveSnapshot snapshot)
    {
        if (snapshot.AppContext.Category == AppCategory.Gaming)
        {
            return ComfortProfileId.Gaming;
        }

        if (snapshot.AppContext.Category == AppCategory.VideoMedia)
        {
            return ComfortProfileId.Video;
        }

        if (snapshot.AppContext.Category == AppCategory.Development)
        {
            return ComfortProfileId.Development;
        }

        if (snapshot.AppContext.Category is AppCategory.Browser or AppCategory.EmailCommunication or AppCategory.OfficeReading
            && snapshot.Content.Classification is LuminanceClassification.Bright or LuminanceClassification.MostlyWhite or LuminanceClassification.HighContrast
            && DayPhasePolicy.GetPhase(snapshot.Now) != DayPhase.Day)
        {
            return ComfortProfileId.Reading;
        }

        return DayPhasePolicy.GetPhase(snapshot.Now) switch
        {
            DayPhase.Day => ComfortProfileId.Day,
            DayPhase.Evening => ComfortProfileId.Evening,
            _ => ComfortProfileId.Night
        };
    }

    private static string BuildReason(AdaptiveSnapshot snapshot, ComfortProfileId profile)
    {
        if (profile == ComfortProfileId.Reading && snapshot.Content.Classification == LuminanceClassification.MostlyWhite)
        {
            return "Bright reading content at night";
        }

        if (profile == ComfortProfileId.Development && snapshot.Now.TimeOfDay < TimeSpan.FromHours(3))
        {
            return "Late development session";
        }

        return profile switch
        {
            ComfortProfileId.Day => "Daylight comfort",
            ComfortProfileId.Evening => "Evening comfort",
            ComfortProfileId.Night => "Night comfort",
            ComfortProfileId.Development => "Development comfort",
            ComfortProfileId.Reading => "Reading comfort",
            _ => "Auto comfort"
        };
    }

    private static int ClampBrightness(int brightness, MonitorModel monitor, UserSettings settings)
    {
        var lower = Math.Max(15, Math.Max(monitor.MinimumBrightnessPercent, settings.MinimumBrightnessPercent));
        var upper = Math.Min(100, Math.Min(monitor.MaximumBrightnessPercent, settings.MaximumBrightnessPercent));
        if (lower > upper)
        {
            lower = upper;
        }

        return Math.Clamp(brightness, lower, upper);
    }

    private static int LimitStep(int current, int target, int maxStep)
    {
        if (current <= 0)
        {
            return target;
        }

        var delta = target - current;
        if (Math.Abs(delta) <= maxStep)
        {
            return target;
        }

        return current + (Math.Sign(delta) * maxStep);
    }

    private static ComfortDecision Decision(ComfortProfileId profile, int brightness, int kelvin, double opacity, bool shouldApply, string reason, string code, UserSettings settings, DecisionSource source = DecisionSource.Default, double confidence = 0.65)
    {
        return new ComfortDecision(profile, brightness, kelvin, opacity, settings.TransitionSpeed, shouldApply, reason, new[] { code }, confidence, source);
    }

    private static double CalculateOverlay(int kelvin)
    {
        return Math.Clamp((6500 - kelvin) / 3700d * 0.14, 0, 0.24);
    }

    private sealed record LightTarget(int BrightnessPercent, int ColorTemperatureKelvin, double OverlayOpacity, double Confidence, DecisionSource Source, bool IsLearned);
}
