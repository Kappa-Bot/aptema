using LightPilot.Application;
using LightPilot.Core;

namespace LightPilot.Application.Tests;

public sealed class CoordinatorTests
{
    [Fact]
    public async Task ExplicitDisplayTestAppliesMildChangeThenRestoresBaseline()
    {
        var output = new RecordingBrightnessController();
        var scheduler = new RecordingScheduler();
        var coordinator = new DisplayTestCoordinator(output, scheduler);
        var baseline = Decision(55);

        var result = await coordinator.TestAsync(Monitor, baseline, UserSettings.Default, CancellationToken.None);

        Assert.True(result.IsUsable);
        Assert.Equal(2, output.Applied.Count);
        Assert.InRange(output.Applied[0].Decision.TargetBrightnessPercent, 49, 52);
        Assert.Equal(55, output.Applied[1].Decision.TargetBrightnessPercent);
        Assert.Equal(TimeSpan.FromSeconds(2), scheduler.LastDelay);
    }

    [Fact]
    public async Task ExplicitDisplayTestRestoresEvenWhenBaselineWasSuppressed()
    {
        var output = new RecordingBrightnessController();
        var coordinator = new DisplayTestCoordinator(output, new RecordingScheduler());
        var baseline = Decision(58) with { ShouldApply = false };

        await coordinator.TestAsync(Monitor, baseline, UserSettings.Default, CancellationToken.None);

        Assert.Equal(2, output.Applied.Count);
        Assert.All(output.Applied, item => Assert.True(item.Decision.ShouldApply));
        Assert.Equal(58, output.Applied[1].Decision.TargetBrightnessPercent);
    }

    private sealed class RecordingScheduler : IAdaptiveScheduler
    {
        public TimeSpan LastDelay { get; private set; }
        public ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            LastDelay = delay;
            return ValueTask.CompletedTask;
        }
    }
    [Fact]
    public async Task DisplayLifecycleMatchesConfigurationByLegacyAliasAfterStableReconnect()
    {
        var monitor = new MonitorModel("display:stable", "Dell", true, true, 15, 100, 0, 11,
            new DisplayBounds(0, 0, 1920, 1080), new DisplayBounds(0, 0, 1920, 1040), true,
            ["device:\\\\.\\DISPLAY1"], "Dell U2723QE");
        var settings = UserSettings.Default with
        {
            DisplayConfigurations = [new DisplayConfiguration("old-stable-id", ["device:\\\\.\\DISPLAY1"], true, -5, 28, 86, true)]
        };
        var observer = new RecordingTopologyObserver();
        var coordinator = new DisplayLifecycleCoordinator(new StubMonitorEnumerator([monitor]), observer);

        var result = await coordinator.LoadAsync(settings, CancellationToken.None);

        var actual = Assert.Single(result.Value!);
        Assert.Equal(-5, actual.BrightnessOffsetPercent);
        Assert.Equal(28, actual.MinimumBrightnessPercent);
        Assert.Equal(86, actual.MaximumBrightnessPercent);
        Assert.Single(observer.LastDisplays!);
    }

    private sealed class RecordingTopologyObserver : IDisplayTopologyObserver
    {
        public IReadOnlyList<MonitorModel>? LastDisplays { get; private set; }
        public ValueTask UpdateTopologyAsync(IReadOnlyList<MonitorModel> displays, CancellationToken cancellationToken)
        {
            LastDisplays = displays;
            return ValueTask.CompletedTask;
        }
    }
    private static readonly MonitorModel Monitor = new("display-1", "Display 1", false, true, 15, 100, 0);
    private static readonly MonitorModel SecondMonitor = new("display-2", "Display 2", false, true, 15, 100, 0);

    [Fact]
    public async Task DisplayLifecycleAppliesStoredMonitorOffset()
    {
        var source = new StubMonitorEnumerator([Monitor]);
        var coordinator = new DisplayLifecycleCoordinator(source);
        var settings = UserSettings.Default with
        {
            MonitorPreferences = [new MonitorPreference { MonitorId = Monitor.Id, BrightnessOffsetPercent = -4 }]
        };

        var result = await coordinator.LoadAsync(settings, CancellationToken.None);

        Assert.True(result.IsUsable);
        Assert.Equal(-4, Assert.Single(result.Value!).BrightnessOffsetPercent);
    }

    [Fact]
    public async Task ConfigurationCoordinatorReturnsUnavailableWhenStoreCannotLoad()
    {
        var coordinator = new ConfigurationCoordinator(new StubSettingsStore(loadError: new IOException("denied")));

        var result = await coordinator.LoadAsync(CancellationToken.None);

        Assert.Equal(OperationStatus.Unavailable, result.Status);
        Assert.Equal("SettingsUnavailable", result.Code);
    }

    [Fact]
    public async Task FeedbackCoordinatorRecordsAndAppliesCorrection()
    {
        var store = new StubSettingsStore();
        var output = new RecordingBrightnessController();
        var coordinator = new FeedbackCoordinator(store, output, new FixedClock());
        var request = new FeedbackRequest(
            ComfortFeedback.TooBright,
            UserSettings.Default,
            [Monitor],
            new AppContextModel("chrome.exe", AppCategory.Browser, false),
            ContentLuminanceSample.Unknown,
            62,
            5200,
            TimeSpan.FromMinutes(20));

        var result = await coordinator.ApplyAsync(request, CancellationToken.None);

        Assert.True(result.IsUsable);
        Assert.Equal(56, result.Value!.Decision.TargetBrightnessPercent);
        Assert.Single(result.Value.ApplyResults);
        Assert.NotEmpty(store.Saved!.PreferenceLearning.Aggregates);
    }

    [Fact]
    public async Task UndoReportsDegradedWhenThrottledDisplayCannotBeCompensated()
    {
        var store = new StubSettingsStore();
        var output = new FixedResultBrightnessController(new BrightnessApplyResult(
            Monitor.Id, BrightnessControlLayer.None, BrightnessControlLayer.None, MonitorControlState.Throttled, "WriteThrottled"));
        var coordinator = new FeedbackCoordinator(store, output, new FixedClock());

        var result = await coordinator.UndoAsync(UndoRequest([Monitor]), CancellationToken.None);

        Assert.Equal(OperationStatus.Degraded, result.Status);
        Assert.Equal("FeedbackUndoCompensationFailed", result.Code);
        Assert.Equal(70, store.Saved!.ComfortIntensity);
    }

    [Fact]
    public async Task UndoRetriesThroughSafeFallbackBeforeRestoringSettings()
    {
        var store = new StubSettingsStore();
        var output = new UndoFallbackBrightnessController();
        var coordinator = new FeedbackCoordinator(store, output, new FixedClock());

        var result = await coordinator.UndoAsync(UndoRequest([Monitor]), CancellationToken.None);

        Assert.True(result.IsUsable);
        Assert.Equal(1, output.FallbackCalls);
        Assert.NotNull(store.Saved);
    }

    [Fact]
    public async Task UndoCompensatesRestoredDisplaysAndSettingsAfterPartialDisplayFailure()
    {
        var previousSettings = UserSettings.Default;
        var postSettings = previousSettings with { ComfortIntensity = 70 };
        var previousDecision = Decision(55);
        var postDecision = Decision(49);
        var store = new TransactionalSettingsStore(postSettings);
        var output = new TransactionalBrightnessController(
            [Monitor, SecondMonitor],
            postDecision,
            failPreviousOnMonitor: SecondMonitor.Id);
        var coordinator = new FeedbackCoordinator(store, output, new FixedClock());

        var result = await coordinator.UndoAsync(
            new FeedbackUndoRequest(previousSettings, postSettings, [Monitor, SecondMonitor], previousDecision, postDecision),
            CancellationToken.None);

        Assert.Equal(OperationStatus.Unavailable, result.Status);
        Assert.Equal("FeedbackUndoCompensated", result.Code);
        Assert.Equal(postSettings, store.Persisted);
        Assert.Equal([previousSettings, postSettings], store.SuccessfulSaves);
        Assert.All(output.VisibleDecisions.Values, decision => Assert.Equal(postDecision, decision));
        Assert.Equal(
            [(Monitor.Id, 55), (SecondMonitor.Id, 55), (Monitor.Id, 49), (SecondMonitor.Id, 49)],
            output.Calls.Select(call => (call.MonitorId, call.Decision.TargetBrightnessPercent)));
    }

    [Fact]
    public async Task UndoCompensatesCurrentDisplayWhenFailedResultFollowsVisibleSideEffect()
    {
        var previousSettings = UserSettings.Default;
        var postSettings = previousSettings with { ComfortIntensity = 70 };
        var previousDecision = Decision(55);
        var postDecision = Decision(49);
        var store = new TransactionalSettingsStore(postSettings);
        var output = new TransactionalBrightnessController(
            [Monitor, SecondMonitor],
            postDecision,
            failPreviousAfterApplyOnMonitor: SecondMonitor.Id);
        var coordinator = new FeedbackCoordinator(store, output, new FixedClock());

        var result = await coordinator.UndoAsync(
            new FeedbackUndoRequest(previousSettings, postSettings, [Monitor, SecondMonitor], previousDecision, postDecision),
            CancellationToken.None);

        Assert.Equal(OperationStatus.Unavailable, result.Status);
        Assert.Equal("FeedbackUndoCompensated", result.Code);
        Assert.Equal(postSettings, store.Persisted);
        Assert.All(output.VisibleDecisions.Values, decision => Assert.Equal(postDecision, decision));
        Assert.Equal(
            [(Monitor.Id, 55), (SecondMonitor.Id, 55), (Monitor.Id, 49), (SecondMonitor.Id, 49)],
            output.Calls.Select(call => (call.MonitorId, call.Decision.TargetBrightnessPercent)));
    }

    [Fact]
    public async Task UndoDoesNotTouchDisplaysWhenPreviousSettingsCannotBePersisted()
    {
        var previousSettings = UserSettings.Default;
        var postSettings = previousSettings with { ComfortIntensity = 70 };
        var postDecision = Decision(49);
        var store = new TransactionalSettingsStore(postSettings, failOnSaveCall: 1);
        var output = new TransactionalBrightnessController([Monitor, SecondMonitor], postDecision);
        var coordinator = new FeedbackCoordinator(store, output, new FixedClock());

        var result = await coordinator.UndoAsync(
            new FeedbackUndoRequest(previousSettings, postSettings, [Monitor, SecondMonitor], Decision(55), postDecision),
            CancellationToken.None);

        Assert.Equal(OperationStatus.Unavailable, result.Status);
        Assert.Equal("FeedbackUndoPersistenceUnavailable", result.Code);
        Assert.Equal(postSettings, store.Persisted);
        Assert.Empty(output.Calls);
        Assert.All(output.VisibleDecisions.Values, decision => Assert.Equal(postDecision, decision));
    }

    [Fact]
    public async Task UndoReportsDegradedWhenDisplayCompensationFails()
    {
        var previousSettings = UserSettings.Default;
        var postSettings = previousSettings with { ComfortIntensity = 70 };
        var previousDecision = Decision(55);
        var postDecision = Decision(49);
        var store = new TransactionalSettingsStore(postSettings);
        var output = new TransactionalBrightnessController(
            [Monitor, SecondMonitor],
            postDecision,
            failPreviousOnMonitor: SecondMonitor.Id,
            failPostOnMonitor: Monitor.Id);
        var coordinator = new FeedbackCoordinator(store, output, new FixedClock());

        var result = await coordinator.UndoAsync(
            new FeedbackUndoRequest(previousSettings, postSettings, [Monitor, SecondMonitor], previousDecision, postDecision),
            CancellationToken.None);

        Assert.Equal(OperationStatus.Degraded, result.Status);
        Assert.Equal("FeedbackUndoCompensationFailed", result.Code);
        Assert.False(result.IsUsable);
        Assert.Equal(postSettings, store.Persisted);
    }

    [Fact]
    public async Task ComfortAutomationProducesSnapshotAndPublishesLatestContext()
    {
        var updates = new LatestValueChannel<ComfortContextUpdate>();
        var output = new RecordingBrightnessController();
        var coordinator = new ComfortAutomationCoordinator(
            new StubForegroundDetector(new AppContextModel("chrome.exe", AppCategory.Browser, false)),
            new StubLuminanceSampler(ContentLuminanceSample.Unknown),
            output,
            new StubPowerStatusProvider(false),
            new FixedClock(),
            updates);
        var request = new ComfortRefreshRequest(UserSettings.Default, [Monitor], null, null, TimeSpan.FromMinutes(20));

        var result = await coordinator.RefreshAsync(request, CancellationToken.None);

        Assert.True(result.IsUsable);
        Assert.Single(result.Value!.Displays);
        Assert.Equal("chrome.exe", result.Value.AppContext.ProcessName);
        Assert.Equal("chrome.exe", (await updates.ReadAsync(CancellationToken.None)).AppContext.ProcessName);
        Assert.Single(output.Applied);
    }

    [Fact]
    public async Task ComfortAutomationUsesAptemaIdentityWhenAutoIsOff()
    {
        var coordinator = new ComfortAutomationCoordinator(
            new StubForegroundDetector(new AppContextModel("chrome.exe", AppCategory.Browser, false)),
            new StubLuminanceSampler(ContentLuminanceSample.Unknown),
            new RecordingBrightnessController(),
            new StubPowerStatusProvider(false),
            new FixedClock(),
            new LatestValueChannel<ComfortContextUpdate>());

        var result = await coordinator.RefreshAsync(
            new ComfortRefreshRequest(UserSettings.Default with { AutoEnabled = false }, [Monitor], null, null, TimeSpan.Zero),
            CancellationToken.None);

        Assert.Equal("Aptema.exe", result.Value!.AppContext.ProcessName);
    }

    [Fact]
    public async Task ComfortAutomationCarriesDisplayFailureAcrossThrottledCycle()
    {
        var previousResult = new BrightnessApplyResult(Monitor.Id, BrightnessControlLayer.DdcCi, BrightnessControlLayer.None, MonitorControlState.Failed, "DdcFailed");
        var previous = new ComfortRuntimeSnapshot(
            new FixedClock().UtcNow,
            new AppContextModel("chrome.exe", AppCategory.Browser, false),
            ContentLuminanceSample.Unknown,
            [new DisplayRuntimeState(Monitor, Decision(), previousResult)],
            Decision(), null, LearningSummary.Empty,
            new SystemHealthState(true, [$"Display:{Monitor.Id}:DdcFailed"]));
        var throttled = new BrightnessApplyResult(Monitor.Id, BrightnessControlLayer.None, BrightnessControlLayer.None, MonitorControlState.Throttled, "WriteThrottled");
        var coordinator = new ComfortAutomationCoordinator(
            new StubForegroundDetector(new AppContextModel("chrome.exe", AppCategory.Browser, false)),
            new StubLuminanceSampler(ContentLuminanceSample.Unknown),
            new FixedResultBrightnessController(throttled),
            new StubPowerStatusProvider(false), new FixedClock(), new LatestValueChannel<ComfortContextUpdate>());

        var result = await coordinator.RefreshAsync(new ComfortRefreshRequest(UserSettings.Default, [Monitor], previous, null, TimeSpan.FromMinutes(20)), CancellationToken.None);

        Assert.True(result.Value!.Health.IsDegraded);
        Assert.Contains($"Display:{Monitor.Id}:DdcFailed", result.Value.Health.Issues);
    }

    [Fact]
    public async Task ComfortAutomationMarksFallbackAsDegradedBeforeThrottleCycle()
    {
        var fallback = new BrightnessApplyResult(
            Monitor.Id,
            BrightnessControlLayer.DdcCi,
            BrightnessControlLayer.WindowsBrightness,
            MonitorControlState.FallbackUsed,
            "HardwareFallbackApplied",
            UsedHardware: true);
        var coordinator = new ComfortAutomationCoordinator(
            new StubForegroundDetector(new AppContextModel("chrome.exe", AppCategory.Browser, false)),
            new StubLuminanceSampler(ContentLuminanceSample.Unknown),
            new FixedResultBrightnessController(fallback),
            new StubPowerStatusProvider(false), new FixedClock(), new LatestValueChannel<ComfortContextUpdate>());

        var result = await coordinator.RefreshAsync(
            new ComfortRefreshRequest(UserSettings.Default, [Monitor], null, null, TimeSpan.FromMinutes(20)),
            CancellationToken.None);

        Assert.True(result.Value!.Health.IsDegraded);
        Assert.Contains($"Display:{Monitor.Id}:HardwareFallbackApplied", result.Value.Health.Issues);
    }

    [Fact]
    public async Task MaintenanceRejectsBlankImportPathBeforeCallingTransferService()
    {
        var transfer = new StubConfigurationTransferService();
        var coordinator = new MaintenanceCoordinator(transfer);

        var result = await coordinator.PreviewImportAsync(" ", CancellationToken.None);

        Assert.Equal(OperationStatus.ValidationFailure, result.Status);
        Assert.Equal(0, transfer.PreviewCalls);
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 7, 22, 18, 0, 0, TimeSpan.Zero);
        public DateTimeOffset LocalNow => UtcNow.ToLocalTime();
    }

    private sealed class StubMonitorEnumerator(IReadOnlyList<MonitorModel> monitors) : IMonitorEnumerator
    {
        public ValueTask<IReadOnlyList<MonitorModel>> EnumerateAsync(CancellationToken cancellationToken) => ValueTask.FromResult(monitors);
    }

    private sealed class StubSettingsStore(Exception? loadError = null) : ISettingsStore
    {
        public UserSettings? Saved { get; private set; }

        public ValueTask<UserSettings> LoadAsync(CancellationToken cancellationToken) =>
            loadError is null ? ValueTask.FromResult(UserSettings.Default) : ValueTask.FromException<UserSettings>(loadError);

        public ValueTask SaveAsync(UserSettings settings, CancellationToken cancellationToken)
        {
            Saved = settings;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingBrightnessController : IBrightnessController
    {
        public List<(MonitorModel Monitor, ComfortDecision Decision)> Applied { get; } = [];

        public ValueTask<BrightnessApplyResult> ApplyAsync(MonitorModel monitor, ComfortDecision decision, UserSettings settings, CancellationToken cancellationToken)
        {
            Applied.Add((monitor, decision));
            return ValueTask.FromResult(new BrightnessApplyResult(monitor.Id, BrightnessControlLayer.Overlay, BrightnessControlLayer.Overlay, MonitorControlState.Ready, "Applied"));
        }
    }

    private sealed class FixedResultBrightnessController(BrightnessApplyResult result) : IBrightnessController
    {
        public ValueTask<BrightnessApplyResult> ApplyAsync(MonitorModel monitor, ComfortDecision decision, UserSettings settings, CancellationToken cancellationToken) =>
            ValueTask.FromResult(result);
    }

    private sealed class UndoFallbackBrightnessController : IBrightnessController, IUndoBrightnessController
    {
        public int FallbackCalls { get; private set; }
        public ValueTask<BrightnessApplyResult> ApplyAsync(MonitorModel monitor, ComfortDecision decision, UserSettings settings, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new BrightnessApplyResult(monitor.Id, BrightnessControlLayer.None, BrightnessControlLayer.None, MonitorControlState.Throttled, "WriteThrottled"));
        public ValueTask<BrightnessApplyResult> ApplyUndoAsync(MonitorModel monitor, ComfortDecision decision, UserSettings settings, DateTimeOffset? retryAfter, CancellationToken cancellationToken)
        {
            FallbackCalls++;
            return ValueTask.FromResult(new BrightnessApplyResult(monitor.Id, BrightnessControlLayer.Overlay, BrightnessControlLayer.Overlay, MonitorControlState.FallbackUsed, "UndoFallbackApplied", UsedOverlay: true));
        }
    }

    private static FeedbackUndoRequest UndoRequest(IReadOnlyList<MonitorModel> displays)
    {
        var postSettings = UserSettings.Default with { ComfortIntensity = 70 };
        return new FeedbackUndoRequest(UserSettings.Default, postSettings, displays, Decision(55), Decision(49));
    }

    private static ComfortDecision Decision(int brightness = 55) => new(
        ComfortProfileId.Evening, brightness, 4800, 0.08, TimeSpan.FromSeconds(45), true, "test", []);

    private sealed class TransactionalSettingsStore(UserSettings initial, int? failOnSaveCall = null) : ISettingsStore
    {
        private int _saveCalls;
        public UserSettings Persisted { get; private set; } = initial;
        public List<UserSettings> SuccessfulSaves { get; } = [];

        public ValueTask<UserSettings> LoadAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Persisted);

        public ValueTask SaveAsync(UserSettings settings, CancellationToken cancellationToken)
        {
            _saveCalls++;
            if (_saveCalls == failOnSaveCall)
            {
                throw new IOException("settings unavailable");
            }

            Persisted = settings;
            SuccessfulSaves.Add(settings);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TransactionalBrightnessController(
        IReadOnlyList<MonitorModel> monitors,
        ComfortDecision initialDecision,
        string? failPreviousOnMonitor = null,
        string? failPostOnMonitor = null,
        string? failPreviousAfterApplyOnMonitor = null) : IBrightnessController
    {
        public Dictionary<string, ComfortDecision> VisibleDecisions { get; } =
            monitors.ToDictionary(monitor => monitor.Id, _ => initialDecision, StringComparer.OrdinalIgnoreCase);
        public List<(string MonitorId, ComfortDecision Decision)> Calls { get; } = [];

        public ValueTask<BrightnessApplyResult> ApplyAsync(
            MonitorModel monitor,
            ComfortDecision decision,
            UserSettings settings,
            CancellationToken cancellationToken)
        {
            Calls.Add((monitor.Id, decision));
            var shouldFail =
                (decision.TargetBrightnessPercent == 55 && monitor.Id == failPreviousOnMonitor) ||
                (decision.TargetBrightnessPercent == 49 && monitor.Id == failPostOnMonitor);
            var shouldFailAfterApply =
                decision.TargetBrightnessPercent == 55 && monitor.Id == failPreviousAfterApplyOnMonitor;
            if (shouldFailAfterApply)
            {
                VisibleDecisions[monitor.Id] = decision;
            }

            if (shouldFail || shouldFailAfterApply)
            {
                return ValueTask.FromResult(new BrightnessApplyResult(
                    monitor.Id,
                    BrightnessControlLayer.Overlay,
                    BrightnessControlLayer.None,
                    MonitorControlState.Failed,
                    "ApplyFailed"));
            }

            VisibleDecisions[monitor.Id] = decision;
            return ValueTask.FromResult(new BrightnessApplyResult(
                monitor.Id,
                BrightnessControlLayer.Overlay,
                BrightnessControlLayer.Overlay,
                MonitorControlState.Ready,
                "Applied",
                UsedOverlay: true));
        }
    }

    private sealed class StubForegroundDetector(AppContextModel context) : IForegroundWindowDetector
    {
        public AppContextModel Detect() => context;
    }

    private sealed class StubLuminanceSampler(ContentLuminanceSample sample) : IContentLuminanceSampler
    {
        public ValueTask<ContentLuminanceSample> SampleAsync(bool enabled, CancellationToken cancellationToken) => ValueTask.FromResult(sample);
    }

    private sealed class StubPowerStatusProvider(bool isOnBattery) : IPowerStatusProvider
    {
        public bool IsOnBattery() => isOnBattery;
    }

    private sealed class StubConfigurationTransferService : IConfigurationTransferService
    {
        public int PreviewCalls { get; private set; }

        public ValueTask<ConfigurationImportPreview> PreviewImportAsync(string path, CancellationToken cancellationToken)
        {
            PreviewCalls++;
            return ValueTask.FromResult(new ConfigurationImportPreview(true, 4, "Valid", []));
        }

        public ValueTask ExportAsync(string path, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask ImportAsync(string path, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
