using System.Windows;
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
    private TrayIconService? _trayIcon;
    private WpfOverlayController? _overlayController;

    public bool IsExplicitShutdown { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
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
        IBrightnessController brightnessController = noHardware
            ? new NoOpBrightnessController()
            : new BrightnessController(
                new DdcCiApi(),
                new WindowsBrightnessApi(),
                _overlayController);

        _viewModel = new MainWindowViewModel(
            settingsStore,
            new MonitorEnumerator(),
            new ForegroundWindowDetector(),
            new ContentLuminanceSampler(),
            brightnessController,
            new SystemPowerStatusProvider(),
            initialSettings)
        {
            StartWithWindows = _startupRegistration.IsEnabled()
        };
        _mainWindow = new MainWindow(_viewModel);
        _trayIcon = new TrayIconService(_viewModel, _mainWindow);

        _viewModel.RequestSettings += (_, _) => ShowSettingsWindow();
        _viewModel.RequestExit += (_, _) => ExitApplication();

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
        _trayIcon?.Dispose();
        _overlayController?.Dispose();
        _singleInstanceGuard?.Dispose();
        base.OnExit(e);
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
            var executablePath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(executablePath))
            {
                _startupRegistration.SetEnabled(settingsWindow.StartWithWindows, executablePath);
            }
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

        var onboarding = new OnboardingWindow();
        if (onboarding.ShowDialog() == true)
        {
            _viewModel.ApplySettings(initialSettings with { HasCompletedOnboarding = true }, _viewModel.StartWithWindows);
        }
    }

    private void ExitApplication()
    {
        IsExplicitShutdown = true;
        _trayIcon?.Dispose();
        Shutdown();
    }
}
