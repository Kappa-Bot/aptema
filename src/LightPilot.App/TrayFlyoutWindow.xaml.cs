using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LightPilot.App.Presentation;
using LightPilot.App.Services;
using LightPilot.App.ViewModels;

namespace LightPilot.App;

public partial class TrayFlyoutWindow : Window
{
    private readonly TrayCommandRouter _router;
    private readonly Func<PixelSize, PixelPoint> _placement;

    public TrayFlyoutWindow(
        MainWindowViewModel viewModel,
        TrayCommandRouter router,
        Func<PixelSize, PixelPoint> placement)
    {
        InitializeComponent();
        _router = router;
        _placement = placement;
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

        Opacity = 0;
        Show();
        UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(this);
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : DesiredSize.Height;
        var point = _placement(new PixelSize(
            (int)Math.Ceiling(width * dpi.DpiScaleX),
            (int)Math.Ceiling(height * dpi.DpiScaleY)));
        Left = point.X / dpi.DpiScaleX;
        Top = point.Y / dpi.DpiScaleY;
        Opacity = 1;
        Activate();
    }

    private void Open_Click(object sender, RoutedEventArgs e) => _router.Execute(TrayMenuCommandKey.Open);
    private void Settings_Click(object sender, RoutedEventArgs e) => _router.Execute(TrayMenuCommandKey.Settings);
}
