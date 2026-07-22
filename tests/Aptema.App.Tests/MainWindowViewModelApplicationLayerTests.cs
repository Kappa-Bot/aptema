using Aptema.Application;
using Aptema.App.ViewModels;
using Aptema.Core;
using Aptema.App.Presentation;

namespace Aptema.App.Tests;

public sealed class MainWindowViewModelApplicationLayerTests
{
    [Fact]
    public void ViewModelStartsFromApplicationRuntimeSnapshot()
    {
        var session = new StubComfortSession();
        var viewModel = new MainWindowViewModel(session, UserSettings.Default);

        Assert.NotNull(viewModel.RuntimeSnapshot);
        Assert.DoesNotContain(
            typeof(MainWindowViewModel).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic),
            field => field.FieldType.FullName == "System.Windows.Threading.DispatcherTimer");

        viewModel.PauseThirtyMinutesCommand.Execute(null);

        Assert.Equal(TimeSpan.FromMinutes(30), session.LastPauseDuration);
    }

    [Fact]
    public void ShellNavigationCommandSelectsEveryProductSurface()
    {
        var viewModel = new MainWindowViewModel(new StubComfortSession(), UserSettings.Default);

        viewModel.SelectSurfaceCommand.Execute(ShellSurface.Learning);

        Assert.Equal(ShellSurface.Learning, viewModel.SelectedSurface);
        Assert.Equal("Learning", viewModel.CurrentSurface.Title);
    }

    [Fact]
    public async Task FeedbackRaisesOsdSignalOnlyAfterSessionAcceptsIt()
    {
        var session = new StubComfortSession();
        var viewModel = new MainWindowViewModel(session, UserSettings.Default);
        var signal = new TaskCompletionSource<FeedbackPresentationEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.FeedbackApplied += (_, args) => signal.TrySetResult(args);

        viewModel.TooBrightCommand.Execute(null);
        var result = await signal.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(ComfortFeedback.TooBright, session.LastFeedback);
        Assert.Equal("A little softer", result.Message);
        Assert.True(result.CanUndo);
    }

    [Fact]
    public void CancelingSystemDraftDiscardsChangesBeforeReopening()
    {
        var viewModel = new MainWindowViewModel(new StubComfortSession(), UserSettings.Default);
        viewModel.SelectSurfaceCommand.Execute(ShellSurface.System);
        viewModel.SettingsDraft!.ComfortIntensity = 90;

        viewModel.CancelSettingsCommand.Execute(null);
        viewModel.SelectSurfaceCommand.Execute(ShellSurface.System);

        Assert.Equal(UserSettings.Default.ComfortIntensity, viewModel.SettingsDraft!.ComfortIntensity);
    }

    [Fact]
    public void PrimaryActionResumesWhenPaused()
    {
        var session = new StubComfortSession(paused: true);
        var viewModel = new MainWindowViewModel(session, UserSettings.Default);

        viewModel.PauseResumeCommand.Execute(null);

        Assert.True(session.ResumeCalled);
        Assert.Equal("Resume", viewModel.PrimaryPauseText);
    }

    [Fact]
    public void OwnProcessUsesAptemaHumanCopy()
    {
        Assert.Equal("Aptema", ApplicationDisplayNamePolicy.GetDisplayName("Aptema.App.exe"));
        Assert.Equal("Visual Studio Code", ApplicationDisplayNamePolicy.GetDisplayName("code.exe"));
    }

    [Fact]
    public void DisplaySurfaceUsesStagedSaveAndCancel()
    {
        var monitor = new MonitorModel("display:one", "Dell", true, true, 15, 100, 0, 11,
            new DisplayBounds(0, 0, 1920, 1080), new DisplayBounds(0, 0, 1920, 1040), true,
            ["device:\\\\.\\DISPLAY1"], "Dell U2723QE");
        var session = new StubComfortSession(displays: [monitor]);
        var viewModel = new MainWindowViewModel(session, UserSettings.Default);

        viewModel.SelectSurfaceCommand.Execute(ShellSurface.Displays);
        viewModel.DisplaySettingsDraft!.Displays[0].IsEnabled = false;
        viewModel.CancelDisplaySettingsCommand.Execute(null);
        Assert.Equal(0, session.ApplySettingsCalls);

        viewModel.SelectSurfaceCommand.Execute(ShellSurface.Displays);
        viewModel.DisplaySettingsDraft!.Displays[0].IsEnabled = false;
        viewModel.SaveDisplaySettingsCommand.Execute(null);
        Assert.Equal(1, session.ApplySettingsCalls);
        Assert.False(session.Settings.DisplayConfigurations[0].IsEnabled);
    }

    private sealed class StubComfortSession : IComfortSession
    {
        public StubComfortSession(bool paused = false, IReadOnlyList<MonitorModel>? displays = null)
        {
            if (displays is not null)
            {
                var decision = new ComfortDecision(ComfortProfileId.Auto, 60, 5200, 0, TimeSpan.Zero, false, "steady", []);
                CurrentSnapshot = new ComfortRuntimeSnapshot(DateTimeOffset.UtcNow,
                    new AppContextModel("Aptema.exe", AppCategory.System, false), ContentLuminanceSample.Unknown,
                    displays.Select(monitor => new DisplayRuntimeState(monitor, decision, BrightnessApplyResult.NoChange(monitor.Id))),
                    decision, null, LearningSummary.Empty, SystemHealthState.Healthy);
            }
            if (paused)
            {
                CurrentSnapshot = CurrentSnapshot with { PausedUntil = DateTimeOffset.UtcNow.AddMinutes(30) };
            }
        }
        event Action<ComfortRuntimeSnapshot>? IComfortSession.SnapshotChanged
        {
            add { }
            remove { }
        }
        public UserSettings Settings { get; private set; } = UserSettings.Default;
        public ComfortRuntimeSnapshot CurrentSnapshot { get; private set; } = ComfortRuntimeSnapshot.Empty(DateTimeOffset.UtcNow);
        public TimeSpan? LastPauseDuration { get; private set; }
        public ComfortFeedback? LastFeedback { get; private set; }
        public bool ResumeCalled { get; private set; }
        public int ApplySettingsCalls { get; private set; }

        public ValueTask StartAsync(UserSettings? initialSettings, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask StopAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask RequestRefreshAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask SetAutoEnabledAsync(bool enabled, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask PauseForAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            LastPauseDuration = duration;
            return ValueTask.CompletedTask;
        }
        public ValueTask PauseUntilTomorrowAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask ResumeAsync(CancellationToken cancellationToken)
        {
            ResumeCalled = true;
            return ValueTask.CompletedTask;
        }
        public ValueTask ResetDefaultsAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask ResetComfortAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask ApplyFeedbackAsync(ComfortFeedback feedback, CancellationToken cancellationToken)
        {
            LastFeedback = feedback;
            CurrentSnapshot = CurrentSnapshot with { FeedbackUndoAvailableUntil = DateTimeOffset.UtcNow.AddSeconds(10) };
            return ValueTask.CompletedTask;
        }
        public ValueTask<OperationResult<ComfortRuntimeSnapshot>> UndoFeedbackAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(OperationResult<ComfortRuntimeSnapshot>.Failure(OperationStatus.Unavailable, "NoUndo"));
        public ValueTask ApplySettingsAsync(UserSettings settings, CancellationToken cancellationToken)
        {
            ApplySettingsCalls++;
            Settings = settings;
            return ValueTask.CompletedTask;
        }
    }
}
