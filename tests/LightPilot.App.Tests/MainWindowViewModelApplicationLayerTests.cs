using LightPilot.Application;
using LightPilot.App.ViewModels;
using LightPilot.Core;

namespace LightPilot.App.Tests;

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

    private sealed class StubComfortSession : IComfortSession
    {
        public event Action<ComfortRuntimeSnapshot>? SnapshotChanged;
        public UserSettings Settings { get; private set; } = UserSettings.Default;
        public ComfortRuntimeSnapshot CurrentSnapshot { get; private set; } = ComfortRuntimeSnapshot.Empty(DateTimeOffset.UtcNow);
        public TimeSpan? LastPauseDuration { get; private set; }

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
        public ValueTask ResumeAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask ResetDefaultsAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask ResetComfortAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask ApplyFeedbackAsync(ComfortFeedback feedback, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask ApplySettingsAsync(UserSettings settings, CancellationToken cancellationToken)
        {
            Settings = settings;
            return ValueTask.CompletedTask;
        }
    }
}
