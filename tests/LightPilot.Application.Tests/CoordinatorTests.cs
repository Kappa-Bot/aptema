using LightPilot.Application;
using LightPilot.Core;

namespace LightPilot.Application.Tests;

public sealed class CoordinatorTests
{
    private static readonly MonitorModel Monitor = new("display-1", "Display 1", false, true, 15, 100, 0);

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
