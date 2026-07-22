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
    private IDisposable? _overlayControllerLifetime;
    private StartupHealthGuard? _startupHealthGuard;
    private DisplayTestCoordinator? _displayTestCoordinator;
    private readonly DisplayIdentificationService _displayIdentification = new();
    private readonly SupportBundleService _supportBundleService = new();
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

        _startupHealthGuard = new StartupHealthGuard();
        var startupHealth = _startupHealthGuard.BeginStartup();
        var settingsStore = new JsonSettingsStore();
        var initialSettings = settingsStore.LoadAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
        var noHardware = e.Args.Any(arg => string.Equals(arg, "--no-hardware", StringComparison.OrdinalIgnoreCase));
        var safeMode = e.Args.Any(arg => string.Equals(arg, "--safe-mode", StringComparison.OrdinalIgnoreCase)) ||
            initialSettings.SafeModeEnabled || startupHealth.ShouldStartSafeMode;
        var background = e.Args.Any(arg => string.Equals(arg, "--background", StringComparison.OrdinalIgnoreCase));
        var smokeTest = e.Args.Any(arg => string.Equals(arg, "--smoke-test", StringComparison.OrdinalIgnoreCase));
        IOverlayController overlayController;
        if (safeMode)
        {
            overlayController = new NoOpOverlayController();
        }
        else
        {
            var wpfOverlay = new WpfOverlayController();
            overlayController = wpfOverlay;
            _overlayControllerLifetime = wpfOverlay;
        }

        LightPilot.Application.IBrightnessController brightnessController = noHardware || safeMode
            ? new NoOpBrightnessController()
            : new BrightnessController(
                new DdcCiApi(),
                new WindowsBrightnessApi(),
                overlayController);
        var clock = new TimeProviderClock();
        var monitorEnumerator = new MonitorEnumerator();
        _detectedMonitors = monitorEnumerator.EnumerateAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
        var foregroundWindowDetector = new ForegroundWindowDetector();
        LightPilot.Application.IContentLuminanceSampler contentLuminanceSampler = safeMode
            ? new DisabledContentLuminanceSampler()
            : new ContentLuminanceSampler();
        var powerStatusProvider = new SystemPowerStatusProvider();
        var contextUpdates = new LatestValueChannel<ComfortContextUpdate>();

        var scheduler = new TimeProviderAdaptiveScheduler();
        _displayTestCoordinator = new DisplayTestCoordinator(brightnessController, scheduler);
        _comfortSession = new ComfortSessionCoordinator(
            new ConfigurationCoordinator(settingsStore),
            new DisplayLifecycleCoordinator(monitorEnumerator, overlayController as IDisplayTopologyObserver),
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
        _viewModel = new MainWindowViewModel(_comfortSession, initialSettings, safeMode)
        {
            StartWithWindows = _startupRegistration.IsEnabled()
        };
        _mainWindow = new MainWindow(_viewModel);
        _trayIcon = new TrayIconService(_viewModel, _mainWindow, enableHotkeys: !safeMode);

        _viewModel.RequestStartupRegistrationChanged += (_, enabled) => SetStartupEnabled(enabled);
        _viewModel.RequestExit += (_, _) => ExitApplication();
        _viewModel.RequestIdentifyDisplay += (_, monitor) => IdentifyDisplay(monitor);
        _viewModel.RequestTestDisplay += async (_, monitor) => await TestDisplayAsync(monitor);
        _viewModel.RequestSupportBundle += async (_, _) => await CreateSupportBundleAsync();

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

        _ = MarkStartupHealthyAfterDelayAsync();
        if (smokeTest) _ = ExitSmokeTestAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemParameters.StaticPropertyChanged -= SystemParameters_StaticPropertyChanged;
        _comfortSession?.StopAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
        _trayIcon?.Dispose();
        _overlayControllerLifetime?.Dispose();
        _startupHealthGuard?.MarkHealthy();
        _singleInstanceGuard?.Dispose();
        base.OnExit(e);
    }

    private async Task MarkStartupHealthyAfterDelayAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(10));
        _startupHealthGuard?.MarkHealthy();
    }

    private async Task ExitSmokeTestAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(2));
        await Dispatcher.InvokeAsync(ExitApplication);
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

    private async Task CreateSupportBundleAsync()
    {
        if (_viewModel is null) return;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save Aptema support package",
            Filter = "ZIP package (*.zip)|*.zip",
            FileName = $"Aptema-support-{DateTime.Now:yyyyMMdd-HHmm}.zip",
            AddExtension = true,
            DefaultExt = ".zip"
        };
        if (dialog.ShowDialog(_mainWindow) != true) return;

        var health = _viewModel.RuntimeSnapshot.Health;
        var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.4.0";
        await _supportBundleService.CreateAsync(
            dialog.FileName,
            new SupportBundleSnapshot(version, health.IsDegraded, health.Issues),
            CancellationToken.None);
        System.Windows.MessageBox.Show(_mainWindow!, "Support package created. It contains no screenshots, app names, window titles, or settings.", "Aptema", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExitApplication()
    {
        IsExplicitShutdown = true;
        _trayIcon?.Dispose();
        Shutdown();
    }
}
