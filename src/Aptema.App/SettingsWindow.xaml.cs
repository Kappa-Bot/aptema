using System.Windows;
using Aptema.App.ViewModels;
using Aptema.Core;

namespace Aptema.App;

public partial class SettingsWindow : Window
{
    private readonly UserSettings _currentSettings;
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(UserSettings settings, bool startWithWindows)
    {
        InitializeComponent();
        _currentSettings = settings;
        _viewModel = new SettingsViewModel(settings, startWithWindows);
        DataContext = _viewModel;
    }

    public UserSettings Settings { get; private set; } = UserSettings.Default;

    public bool StartWithWindows { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Settings = _viewModel.ToSettings(_currentSettings);
        StartWithWindows = _viewModel.StartWithWindows;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
