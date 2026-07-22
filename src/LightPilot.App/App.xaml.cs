using System.Windows;
using LightPilot.Application;
using LightPilot.App.Services;
using LightPilot.App.ViewModels;
using LightPilot.Core;
using LightPilot.Infrastructure;

namespace LightPilot.App;

public partial class App : System.Windows.Application
{
    private readonly LightPilot.Infrastructure.StartupRegistrationService _startupRegistration = new();
    private SingleInstanceGuard? _singleInstanceGuard;
    private MainWindow? _mainWindow;
    private MainWindowViewModel? _viewModel;
    private IComfortSession? _comfortSession;
    private TrayIconService? _trayIcon;
    private WpfOverlayController? _overlayController;
    private DisplayTestCoordinator? _displayTestCoordinator;
    private readonly DisplayIdentificationService _displayIdentification = new();
    private IReadOnlyList<LightPilot.Core.MonitorModel> _detectedMonitors = Array.Empty<LightPilot.Core.MonitorModel>();

    public bool IsExplicitShutdown { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeService.ApplySystemTheme(Resources);
        SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        if (!SingleInstanceGuard.TryAcquire(out _singleInstanceGuard))
        {
            Shutdown();
            return;
        }

        _overlayController = new WpfOverlayController();
        var settingsStore = new JsonSettingsStore();
        var initialSettings = settingsStore.LoadAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
        var noHardware = e.Args.Any(arg => string.Equals(arg, "--no-hardware", StringComparison.OrdinalIgnoreCase));
        var background = e.Args.Any(arg => string.Equals(arg, "--background", StringComparison.OrdinalIgnoreCase));
        LightPilot.Application.IBrightnessController brightnessController = noHardware
            ? new NoOpBrightnessController()
            : new BrightnessController(
                new DdcCiApi(),
                new WindowsBrightnessApi(),
                _overlayController);
        var clock = new TimeProviderClock();
        var monitorEnumerator = new MonitorEnumerator();
        _detectedMonitors = monitorEnumerator.EnumerateAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
        var foregroundWindowDetector = new ForegroundWindowDetector();
        var contentLuminanceSampler = new ContentLuminanceSampler();
        var powerStatusProvider = new SystemPowerStatusProvider();
        var contextUpdates = new LatestValueChannel<ComfortContextUpdate>();

        var scheduler = new TimeProviderAdaptiveScheduler();
        _displayTestCoordinator = new DisplayTestCoordinator(brightnessController, scheduler);
        _comfortSession = new ComfortSessionCoordinator(
            new ConfigurationCoordinator(settingsStore),
            new DisplayLifecycleCoordinator(monitorEnumerator, _overlayController),
            new ComfortAutomationCoordinator(
                foregroundWindowDetector,
                contentLuminanceSampler,
                brightnessController,
                powerStatusProvider,
                clock,
                contextUpdates),
            new FeedbackCoordinator(settingsStore, brightnessController, clock),
            brightnessController,
            clock,
            scheduler);
        _viewModel = new MainWindowViewModel(_comfortSession, initialSettings)
        {
            StartWithWindows = _startupRegistration.IsEnabled()
        };
        _mainWindow = new MainWindow(_viewModel);
        _trayIcon = new TrayIconService(_viewModel, _mainWindow);

        _viewModel.RequestStartupRegistrationChanged += (_, enabled) => SetStartupEnabled(enabled);
        _viewModel.RequestExit += (_, _) => ExitApplication();
        _viewModel.RequestIdentifyDisplay += (_, monitor) => IdentifyDisplay(monitor);
        _viewModel.RequestTestDisplay += async (_, monitor) => await TestDisplayAsync(monitor);

        MainWindow = _mainWindow;
        _singleInstanceGuard?.StartActivationListener(() => Dispatcher.Invoke(ShowMainWindow));
        if (!background)
        {
            if (!initialSettings.HasCompletedOnboarding)
            {
                ShowOnboardingWindow(initialSettings);
            }

            ShowMainWindow();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemParameters.StaticPropertyChanged -= SystemParameters_StaticPropertyChanged;
        _comfortSession?.StopAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
        _trayIcon?.Dispose();
        _overlayController?.Dispose();
        _singleInstanceGuard?.Dispose();
        base.OnExit(e);
    }

    private void SystemParameters_StaticPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.HighContrast))
        {
            ThemeService.ApplySystemTheme(Resources);
        }
    }

    private void ShowSettingsWindow()
    {
        if (_viewModel is null || _mainWindow is null)
        {
            return;
        }

        var settingsWindow = new SettingsWindow(_viewModel.Settings, _viewModel.StartWithWindows)
        {
            Owner = _mainWindow
        };

        if (settingsWindow.ShowDialog() == true)
        {
            _viewModel.ApplySettings(settingsWindow.Settings, settingsWindow.StartWithWindows);
            SetStartupEnabled(settingsWindow.StartWithWindows);
        }
    }

    private void SetStartupEnabled(bool enabled)
    {
        var executablePath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            _startupRegistration.SetEnabled(enabled, executablePath);
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        if (!_mainWindow.IsVisible)
        {
            _mainWindow.Show();
        }

        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Activate();
    }

    private void ShowOnboardingWindow(UserSettings initialSettings)
    {
        if (_viewModel is null)
        {
            return;
        }

        var model = new OnboardingViewModel(initialSettings, _detectedMonitors, _viewModel.StartWithWindows);
        model.IdentifyDisplayRequested += (_, monitor) => IdentifyDisplay(monitor);
        model.DisplayTestRequested += async (_, monitor) => await TestDisplayAsync(monitor);
        var onboarding = new OnboardingWindow(model);
        if (onboarding.ShowDialog() == true)
        {
            _viewModel.ApplySettings(model.Complete(initialSettings), model.StartWithWindows);
            SetStartupEnabled(model.StartWithWindows);
        }
    }

    private void IdentifyDisplay(LightPilot.Core.MonitorModel monitor)
    {
        var number = Math.Max(1, _detectedMonitors.ToList().FindIndex(item => item.Id == monitor.Id) + 1);
        _displayIdentification.Show(monitor, number);
    }

    private async Task TestDisplayAsync(LightPilot.Core.MonitorModel monitor)
    {
        if (_displayTestCoordinator is null || _viewModel is null) return;
        var baseline = _viewModel.RuntimeSnapshot.Displays.FirstOrDefault(item => item.Monitor.Id == monitor.Id)?.Decision
            ?? _viewModel.RuntimeSnapshot.PrimaryDecision
            ?? new LightPilot.Core.ComfortDecision(LightPilot.Core.ComfortProfileId.Auto, 60, 5200, 0, TimeSpan.FromSeconds(2), true, "Display check", []);
        await _displayTestCoordinator.TestAsync(monitor, baseline, _viewModel.Settings, CancellationToken.None);
    }

    private void ExitApplication()
    {
        IsExplicitShutdown = true;
        _trayIcon?.Dispose();
        Shutdown();
    }
}
