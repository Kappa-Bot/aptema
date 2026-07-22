using System.Threading.Channels;
using Aptema.Application;
using Aptema.Core;

namespace Aptema.Application.Tests;

public sealed class ComfortSessionCoordinatorTests
{
    private static readonly MonitorModel Monitor = new("display-1", "Display 1", false, true, 15, 100, 0);

    [Fact]
    public async Task StartPerformsInitialRefreshAndSchedulerTriggersNextRefresh()
    {
        var fixture = new SessionFixture();
        await fixture.Session.StartAsync(UserSettings.Default, CancellationToken.None);

        Assert.Single(fixture.Output.Applied);
        fixture.Scheduler.Tick();
        await fixture.Output.WaitForCountAsync(2);

        Assert.Equal(2, fixture.Output.Applied.Count);
        await fixture.Session.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task PauseAndResumeAreOwnedBySession()
    {
        var fixture = new SessionFixture();
        await fixture.Session.StartAsync(UserSettings.Default, CancellationToken.None);

        await fixture.Session.PauseForAsync(TimeSpan.FromMinutes(30), CancellationToken.None);

        Assert.Equal(fixture.Clock.LocalNow.AddMinutes(30), fixture.Session.CurrentSnapshot.PausedUntil);
        Assert.True(fixture.Session.Settings.AutoEnabled);

        await fixture.Session.ResumeAsync(CancellationToken.None);

        Assert.Null(fixture.Session.CurrentSnapshot.PausedUntil);
        await fixture.Session.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ResetComfortUsesOutputAndPausesSessionWithoutViewModelHardwareAccess()
    {
        var fixture = new SessionFixture();
        await fixture.Session.StartAsync(UserSettings.Default, CancellationToken.None);
        fixture.Output.Applied.Clear();

        await fixture.Session.ResetComfortAsync(CancellationToken.None);

        var applied = Assert.Single(fixture.Output.Applied);
        Assert.Equal(6500, applied.Decision.TargetColorTemperatureKelvin);
        Assert.Equal(fixture.Clock.LocalNow.AddMinutes(30), fixture.Session.CurrentSnapshot.PausedUntil);
        await fixture.Session.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task UndoWithinTenSecondsRestoresLearningAndPreviousTarget()
    {
        var fixture = new SessionFixture();
        await fixture.Session.StartAsync(UserSettings.Default, CancellationToken.None);
        var before = fixture.Session.CurrentSnapshot.PrimaryDecision!;

        await fixture.Session.ApplyFeedbackAsync(ComfortFeedback.TooBright, CancellationToken.None);
        Assert.NotEmpty(fixture.Session.Settings.PreferenceLearning.Aggregates);
        Assert.NotNull(fixture.Session.CurrentSnapshot.FeedbackUndoAvailableUntil);

        fixture.Clock.Advance(TimeSpan.FromSeconds(9));
        var result = await fixture.Session.UndoFeedbackAsync(CancellationToken.None);

        Assert.True(result.IsUsable);
        Assert.Empty(fixture.Session.Settings.PreferenceLearning.Aggregates);
        Assert.Equal(before.TargetBrightnessPercent, fixture.Session.CurrentSnapshot.PrimaryDecision!.TargetBrightnessPercent);
        Assert.Null(fixture.Session.CurrentSnapshot.FeedbackUndoAvailableUntil);
        await fixture.Session.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task UndoAfterTenSecondsDoesNotChangeLearningOrOutput()
    {
        var fixture = new SessionFixture();
        await fixture.Session.StartAsync(UserSettings.Default, CancellationToken.None);
        await fixture.Session.ApplyFeedbackAsync(ComfortFeedback.TooBright, CancellationToken.None);
        var writes = fixture.Output.Applied.Count;

        fixture.Clock.Advance(TimeSpan.FromSeconds(11));
        var result = await fixture.Session.UndoFeedbackAsync(CancellationToken.None);

        Assert.Equal(OperationStatus.Unavailable, result.Status);
        Assert.NotEmpty(fixture.Session.Settings.PreferenceLearning.Aggregates);
        Assert.Equal(writes, fixture.Output.Applied.Count);
        await fixture.Session.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FailedUndoPersistenceLeavesPostFeedbackSnapshotAndOutputUntouched()
    {
        var store = new StubSettingsStore(failOnSaveCall: 2);
        var fixture = new SessionFixture(store);
        await fixture.Session.StartAsync(UserSettings.Default, CancellationToken.None);
        await fixture.Session.ApplyFeedbackAsync(ComfortFeedback.TooBright, CancellationToken.None);
        var postFeedbackSnapshot = fixture.Session.CurrentSnapshot;
        var postFeedbackSettings = fixture.Session.Settings;
        var writes = fixture.Output.Applied.Count;

        var result = await fixture.Session.UndoFeedbackAsync(CancellationToken.None);

        Assert.Equal(OperationStatus.Unavailable, result.Status);
        Assert.Same(postFeedbackSnapshot, fixture.Session.CurrentSnapshot);
        Assert.Equal(postFeedbackSettings, fixture.Session.Settings);
        Assert.Equal(postFeedbackSettings, store.Persisted);
        Assert.Equal(writes, fixture.Output.Applied.Count);
        await fixture.Session.StopAsync(CancellationToken.None);
    }

    private sealed class SessionFixture
    {
        public SessionFixture(StubSettingsStore? store = null)
        {
            store ??= new StubSettingsStore();
            Output = new RecordingBrightnessController();
            Clock = new FixedClock();
            Scheduler = new ManualAdaptiveScheduler();
            var displayLifecycle = new DisplayLifecycleCoordinator(new StubMonitorEnumerator());
            var automation = new ComfortAutomationCoordinator(
                new StubForegroundDetector(),
                new StubLuminanceSampler(),
                Output,
                new StubPowerStatusProvider(),
                Clock,
                new LatestValueChannel<ComfortContextUpdate>());
            Session = new ComfortSessionCoordinator(
                new ConfigurationCoordinator(store),
                displayLifecycle,
                automation,
                new FeedbackCoordinator(store, Output, Clock),
                Output,
                Clock,
                Scheduler);
        }

        public ComfortSessionCoordinator Session { get; }
        public RecordingBrightnessController Output { get; }
        public FixedClock Clock { get; }
        public ManualAdaptiveScheduler Scheduler { get; }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = new(2026, 7, 22, 18, 0, 0, TimeSpan.Zero);
        public DateTimeOffset LocalNow => UtcNow;

        public void Advance(TimeSpan duration) => UtcNow += duration;
    }

    private sealed class ManualAdaptiveScheduler : IAdaptiveScheduler
    {
        private readonly Channel<bool> _ticks = Channel.CreateUnbounded<bool>();

        public ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
            new(_ticks.Reader.ReadAsync(cancellationToken).AsTask());

        public void Tick() => _ticks.Writer.TryWrite(true);
    }

    private sealed class StubSettingsStore(int? failOnSaveCall = null) : ISettingsStore
    {
        private int _saveCalls;
        public UserSettings Persisted { get; private set; } = UserSettings.Default;

        public ValueTask<UserSettings> LoadAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Persisted);
        public ValueTask SaveAsync(UserSettings settings, CancellationToken cancellationToken)
        {
            _saveCalls++;
            if (_saveCalls == failOnSaveCall)
            {
                throw new IOException("settings unavailable");
            }

            Persisted = settings;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubMonitorEnumerator : IMonitorEnumerator
    {
        public ValueTask<IReadOnlyList<MonitorModel>> EnumerateAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<MonitorModel>>([Monitor]);
    }

    private sealed class RecordingBrightnessController : IBrightnessController
    {
        private TaskCompletionSource _changed = NewSignal();
        public List<(MonitorModel Monitor, ComfortDecision Decision)> Applied { get; } = [];

        public ValueTask<BrightnessApplyResult> ApplyAsync(MonitorModel monitor, ComfortDecision decision, UserSettings settings, CancellationToken cancellationToken)
        {
            Applied.Add((monitor, decision));
            _changed.TrySetResult();
            return ValueTask.FromResult(new BrightnessApplyResult(
                monitor.Id,
                BrightnessControlLayer.Overlay,
                BrightnessControlLayer.Overlay,
                MonitorControlState.Ready,
                "Applied",
                UsedOverlay: true));
        }

        public async Task WaitForCountAsync(int count)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            while (Applied.Count < count)
            {
                var signal = _changed;
                await signal.Task.WaitAsync(timeout.Token);
                if (ReferenceEquals(signal, _changed))
                {
                    _changed = NewSignal();
                }
            }
        }

        private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class StubForegroundDetector : IForegroundWindowDetector
    {
        public AppContextModel Detect() => new("test.exe", AppCategory.Unknown, false);
    }

    private sealed class StubLuminanceSampler : IContentLuminanceSampler
    {
        public ValueTask<ContentLuminanceSample> SampleAsync(bool enabled, CancellationToken cancellationToken) => ValueTask.FromResult(ContentLuminanceSample.Unknown);
    }

    private sealed class StubPowerStatusProvider : IPowerStatusProvider
    {
        public bool IsOnBattery() => false;
    }
}
