using LightPilot.Core;

namespace LightPilot.Core.Tests;

public sealed class AdaptiveEngineTests
{
    [Fact]
    public void BrightBrowserContentAtNightReducesBrightnessAndAddsWarmth()
    {
        var engine = new AdaptiveEngine();
        var snapshot = TestSnapshots.Default with
        {
            Now = new DateTimeOffset(2026, 5, 7, 23, 30, 0, TimeSpan.Zero),
            AppContext = new AppContextModel("chrome.exe", AppCategory.Browser, isFullscreen: false),
            Content = new ContentLuminanceSample(true, 0.78, 0.66, 0.71, 0.02, LuminanceClassification.MostlyWhite),
            CurrentBrightness = 70
        };

        var decision = engine.Evaluate(snapshot, AdaptiveEngineState.Empty, UserSettings.Default);

        Assert.Equal(ComfortProfileId.Reading, decision.Profile);
        Assert.Equal(67, decision.TargetBrightnessPercent);
        Assert.InRange(decision.TargetColorTemperatureKelvin, 6300, 6499);
        Assert.Equal("Bright reading content at night", decision.Reason);
        Assert.True(decision.ShouldApply);
    }

    [Fact]
    public void FullscreenGameProtectsBrightnessAndUsesMildWarmth()
    {
        var engine = new AdaptiveEngine();
        var snapshot = TestSnapshots.Default with
        {
            Now = new DateTimeOffset(2026, 5, 7, 22, 0, 0, TimeSpan.Zero),
            AppContext = new AppContextModel("Overwatch.exe", AppCategory.Gaming, isFullscreen: true),
            CurrentBrightness = 68,
            CurrentColorTemperatureKelvin = 6500
        };

        var decision = engine.Evaluate(snapshot, AdaptiveEngineState.Empty, UserSettings.Default);

        Assert.Equal(ComfortProfileId.Gaming, decision.Profile);
        Assert.Equal(68, decision.TargetBrightnessPercent);
        Assert.Equal(6500, decision.TargetColorTemperatureKelvin);
        Assert.False(decision.ShouldApply);
        Assert.Equal(DecisionSource.Protected, decision.Source);
        Assert.Equal("Gaming fullscreen protection", decision.Reason);
    }

    [Fact]
    public void LateDevelopmentSessionAppliesModerateReductionAndMediumWarmth()
    {
        var engine = new AdaptiveEngine();
        var snapshot = TestSnapshots.Default with
        {
            Now = new DateTimeOffset(2026, 5, 8, 1, 30, 0, TimeSpan.Zero),
            AppContext = new AppContextModel("Code.exe", AppCategory.Development, isFullscreen: false),
            Content = new ContentLuminanceSample(true, 0.18, 0.01, 0.04, 0.75, LuminanceClassification.Dark),
            CurrentBrightness = 70
        };

        var decision = engine.Evaluate(snapshot, AdaptiveEngineState.Empty, UserSettings.Default);

        Assert.Equal(ComfortProfileId.Development, decision.Profile);
        Assert.Equal(67, decision.TargetBrightnessPercent);
        Assert.InRange(decision.TargetColorTemperatureKelvin, 6300, 6499);
        Assert.Equal("Late development session", decision.Reason);
    }

    [Fact]
    public void FullscreenVideoUsesMinimalAdjustment()
    {
        var engine = new AdaptiveEngine();
        var snapshot = TestSnapshots.Default with
        {
            Now = new DateTimeOffset(2026, 5, 7, 20, 0, 0, TimeSpan.Zero),
            AppContext = new AppContextModel("vlc.exe", AppCategory.VideoMedia, isFullscreen: true),
            CurrentBrightness = 61
        };

        var decision = engine.Evaluate(snapshot, AdaptiveEngineState.Empty, UserSettings.Default);

        Assert.Equal(ComfortProfileId.Video, decision.Profile);
        Assert.Equal(61, decision.TargetBrightnessPercent);
        Assert.False(decision.ShouldApply);
        Assert.Equal(DecisionSource.Protected, decision.Source);
        Assert.Equal("Video playback protected", decision.Reason);
    }

    [Fact]
    public void SafetyClampWinsAfterAllAdjustments()
    {
        var engine = new AdaptiveEngine();
        var settings = UserSettings.Default with { MinimumBrightnessPercent = 35, MaximumBrightnessPercent = 65, ComfortIntensity = 100 };
        var snapshot = TestSnapshots.Default with
        {
            Now = new DateTimeOffset(2026, 5, 8, 2, 15, 0, TimeSpan.Zero),
            AppContext = new AppContextModel("chrome.exe", AppCategory.Browser, isFullscreen: false),
            Content = new ContentLuminanceSample(true, 0.92, 0.85, 0.9, 0, LuminanceClassification.MostlyWhite),
            CurrentBrightness = 80
        };

        var decision = engine.Evaluate(snapshot, AdaptiveEngineState.Empty, settings);

        Assert.InRange(decision.TargetBrightnessPercent, 35, 65);
    }

    [Fact]
    public void AutomaticBrightnessChangeIsLimitedToThreePointsPerDecision()
    {
        var engine = new AdaptiveEngine();
        var snapshot = TestSnapshots.Default with
        {
            Now = new DateTimeOffset(2026, 5, 7, 12, 0, 0, TimeSpan.Zero),
            CurrentBrightness = 40
        };

        var decision = engine.Evaluate(snapshot, AdaptiveEngineState.Empty, UserSettings.Default);

        Assert.Equal(43, decision.TargetBrightnessPercent);
    }

    [Fact]
    public void AutomaticWarmthChangeIsLimitedToTwoHundredKelvinPerDecision()
    {
        var engine = new AdaptiveEngine();
        var snapshot = TestSnapshots.Default with
        {
            Now = new DateTimeOffset(2026, 5, 7, 23, 30, 0, TimeSpan.Zero),
            CurrentColorTemperatureKelvin = 6500,
            AppContext = new AppContextModel("chrome.exe", AppCategory.Browser, isFullscreen: false),
            Content = new ContentLuminanceSample(true, 0.82, 0.74, 0.81, 0, LuminanceClassification.MostlyWhite)
        };

        var decision = engine.Evaluate(snapshot, AdaptiveEngineState.Empty, UserSettings.Default);

        Assert.Equal(6300, decision.TargetColorTemperatureKelvin);
    }

    [Fact]
    public void LearnedOffsetsApplyBeforeStepLimitAndMarkDecisionLearned()
    {
        var engine = new AdaptiveEngine();
        var context = new PreferenceLearningContext("monitor-1", AppCategory.Browser, DayPhase.Night, false, LuminanceClassification.MostlyWhite);
        var learning = PreferenceLearningModel.Empty;
        var now = new DateTimeOffset(2026, 5, 8, 23, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 6; i++)
        {
            learning = PreferenceLearningService.RecordFeedback(learning, context, ComfortFeedback.TooBright, now.AddMinutes(i));
        }

        var settings = UserSettings.Default with { PreferenceLearning = learning };
        var snapshot = TestSnapshots.Default with
        {
            Now = now.AddMinutes(10),
            AppContext = new AppContextModel("chrome.exe", AppCategory.Browser, isFullscreen: false),
            Content = new ContentLuminanceSample(true, 0.82, 0.72, 0.81, 0.02, LuminanceClassification.MostlyWhite),
            CurrentBrightness = 70,
            CurrentColorTemperatureKelvin = 6500
        };

        var decision = engine.Evaluate(snapshot, AdaptiveEngineState.Empty, settings);

        Assert.True(decision.IsLearned);
        Assert.Equal(DecisionSource.Learned, decision.Source);
        Assert.InRange(decision.Confidence, 0.99, 1.0);
        Assert.Equal(67, decision.TargetBrightnessPercent);
        Assert.Equal(6300, decision.TargetColorTemperatureKelvin);
    }

    [Fact]
    public void ManualFeedbackBypassesCooldownWithinSafeCorrectionLimit()
    {
        var engine = new AdaptiveEngine();
        var snapshot = TestSnapshots.Default with
        {
            CurrentBrightness = 70,
            CurrentColorTemperatureKelvin = 5600
        };

        var decision = engine.EvaluateManualFeedback(snapshot, UserSettings.Default, ComfortFeedback.TooBright);

        Assert.True(decision.ShouldApply);
        Assert.Equal(DecisionSource.Manual, decision.Source);
        Assert.Equal(64, decision.TargetBrightnessPercent);
        Assert.Equal(5300, decision.TargetColorTemperatureKelvin);
    }

    [Fact]
    public void ManualFeedbackInProtectedFullscreenAvoidsBrightnessWrite()
    {
        var engine = new AdaptiveEngine();
        var snapshot = TestSnapshots.Default with
        {
            AppContext = new AppContextModel("overwatch.exe", AppCategory.Gaming, isFullscreen: true),
            CurrentBrightness = 72,
            CurrentColorTemperatureKelvin = 6200
        };

        var decision = engine.EvaluateManualFeedback(snapshot, UserSettings.Default, ComfortFeedback.TooBright);

        Assert.True(decision.ShouldApply);
        Assert.Equal(DecisionSource.Manual, decision.Source);
        Assert.Equal(72, decision.TargetBrightnessPercent);
        Assert.Equal(5900, decision.TargetColorTemperatureKelvin);
        Assert.True(decision.OverlayOpacity > 0);
    }

    [Fact]
    public void HysteresisSuppressesTinyChanges()
    {
        var engine = new AdaptiveEngine();
        var snapshot = TestSnapshots.Default with
        {
            Now = new DateTimeOffset(2026, 5, 7, 12, 0, 0, TimeSpan.Zero),
            CurrentBrightness = 80,
            CurrentColorTemperatureKelvin = 6500
        };
        var state = AdaptiveEngineState.Empty with
        {
            LastAppliedAt = snapshot.Now.AddSeconds(-90),
            LastDecision = new LightTarget(79, 6450, 0)
        };

        var decision = engine.Evaluate(snapshot, state, UserSettings.Default);

        Assert.False(decision.ShouldApply);
        Assert.Equal("No visible change needed", decision.Reason);
    }

    [Fact]
    public void CooldownSuppressesFrequentAutomaticChanges()
    {
        var engine = new AdaptiveEngine();
        var snapshot = TestSnapshots.Default with
        {
            Now = new DateTimeOffset(2026, 5, 7, 22, 5, 10, TimeSpan.Zero),
            CurrentBrightness = 80
        };
        var state = AdaptiveEngineState.Empty with
        {
            LastAppliedAt = snapshot.Now.AddSeconds(-10),
            LastDecision = new LightTarget(62, 4100, 0.15)
        };

        var decision = engine.Evaluate(snapshot, state, UserSettings.Default);

        Assert.False(decision.ShouldApply);
        Assert.Equal("Cooling down before next adjustment", decision.Reason);
    }

    [Fact]
    public void ApplicationRuleReclassifiesBeforeFullscreenProtection()
    {
        var engine = new AdaptiveEngine();
        var settings = UserSettings.Default with
        {
            ApplicationRules = [new("game-rule", "Keep this game stable", true, 10, "special.exe", AppCategory.Gaming, ComfortProfileId.Gaming, null, 0, true)]
        };
        var snapshot = TestSnapshots.Default with
        {
            AppContext = new AppContextModel("SPECIAL.EXE", AppCategory.Unknown, isFullscreen: true),
            CurrentBrightness = 61
        };

        var decision = engine.Evaluate(snapshot, AdaptiveEngineState.Empty, settings);

        Assert.False(decision.ShouldApply);
        Assert.Equal(DecisionSource.Protected, decision.Source);
        Assert.Equal(ComfortProfileId.Gaming, decision.Profile);
        Assert.Equal("Keep this game stable", decision.ResponsibleRule);
    }

    [Fact]
    public void ApplicationRuleCanProtectAnyFullscreenApplication()
    {
        var engine = new AdaptiveEngine();
        var settings = UserSettings.Default with
        {
            ApplicationRules = [new("presentation-rule", "Stable presentation", true, 10, "slides.exe", AppCategory.OfficeReading, ComfortProfileId.Reading, null, 0, true)]
        };
        var snapshot = TestSnapshots.Default with
        {
            AppContext = new AppContextModel("slides.exe", AppCategory.Unknown, isFullscreen: true),
            CurrentBrightness = 61
        };

        var decision = engine.Evaluate(snapshot, AdaptiveEngineState.Empty, settings);

        Assert.False(decision.ShouldApply);
        Assert.Equal(DecisionSource.Protected, decision.Source);
        Assert.Equal("Stable presentation", decision.ResponsibleRule);
    }

    [Fact]
    public void CustomProfileAndAutomationProduceExplainableBoundedTarget()
    {
        var engine = new AdaptiveEngine();
        var settings = UserSettings.Default with
        {
            CustomProfiles = [new("soft-code", "Soft coding", 66, 52, 38, 6000, 4700, 3600, 110, true)],
            ApplicationRules = [new("code-rule", "Late coding", true, 20, "code.exe", AppCategory.Development, null, "soft-code", 0, true)],
            AutomationRules = [new("night-code", "Night softening", true, 10, DayPhase.Night, AppCategory.Development, false, LuminanceClassification.Dark, -4, -180)]
        };
        var snapshot = TestSnapshots.Default with
        {
            Now = new DateTimeOffset(2026, 5, 7, 22, 30, 0, TimeSpan.Zero),
            AppContext = new AppContextModel("code.exe", AppCategory.Unknown, false),
            Content = new ContentLuminanceSample(true, 0.12, 0, 0.02, 0.84, LuminanceClassification.Dark),
            CurrentBrightness = 0,
            CurrentColorTemperatureKelvin = 0
        };

        var decision = engine.Evaluate(snapshot, AdaptiveEngineState.Empty, settings);

        Assert.Equal(ComfortProfileId.Development, decision.Profile);
        Assert.Equal("Soft coding", decision.ProfileName);
        Assert.Equal("Late coding", decision.ResponsibleRule);
        Assert.Contains("night-code", decision.ReasonCodes);
        Assert.Equal(34, decision.TargetBrightnessPercent);
        Assert.Equal(3420, decision.TargetColorTemperatureKelvin);
        Assert.Equal(TimeSpan.FromSeconds(110), decision.TransitionDuration);
    }
}

internal static class TestSnapshots
{
    public static AdaptiveSnapshot Default => new(
        Now: new DateTimeOffset(2026, 5, 7, 12, 0, 0, TimeSpan.Zero),
        Monitor: new MonitorModel("monitor-1", "Primary", true, true, 15, 100, 0),
        AppContext: new AppContextModel("notepad.exe", AppCategory.OfficeReading, isFullscreen: false),
        Content: ContentLuminanceSample.Unknown,
        ScreenTimeSessionLength: TimeSpan.FromMinutes(20),
        CurrentBrightness: 70,
        CurrentColorTemperatureKelvin: 6500,
        ManualOverrideUntil: null);
}
