using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LightPilot.App.Services;
using LightPilot.App.ViewModels;
using Forms = System.Windows.Forms;

namespace LightPilot.App;

public partial class TrayFlyoutWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly TrayCommandRouter _router;

    public TrayFlyoutWindow(MainWindowViewModel viewModel, TrayCommandRouter router)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _router = router;
        DataContext = viewModel;
        Deactivated += (_, _) => Hide();
        PreviewKeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                Hide();
            }
        };
    }

    public void ToggleNearTaskbar()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        var cursor = Forms.Cursor.Position;
        var area = Forms.Screen.FromPoint(cursor).WorkingArea;
        Show();
        var dpi = VisualTreeHelper.GetDpi(this);
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight;
        Left = Math.Clamp(cursor.X / dpi.DpiScaleX - width + 28, area.Left / dpi.DpiScaleX + 8, area.Right / dpi.DpiScaleX - width - 8);
        Top = Math.Max(area.Top / dpi.DpiScaleY + 8, area.Bottom / dpi.DpiScaleY - height - 8);
        Activate();
    }

    private void PauseOrResume_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.AutoEnabled || _viewModel.RuntimeSnapshot.PausedUntil is not null)
        {
            _viewModel.ResumeCommand.Execute(null);
        }
        else
        {
            _viewModel.PauseThirtyMinutesCommand.Execute(null);
        }
    }

    private void Open_Click(object sender, RoutedEventArgs e) => _router.Execute(TrayMenuCommandKey.Open);
    private void Settings_Click(object sender, RoutedEventArgs e) => _router.Execute(TrayMenuCommandKey.Settings);
}
