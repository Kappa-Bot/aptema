using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;
using Aptema.App.ViewModels;
using Aptema.App.Services;

namespace Aptema.App;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.SelectedSurface) || ThemeService.IsReducedMotion)
        {
            return;
        }

        var duration = new Duration(TimeSpan.FromMilliseconds(160));
        ContentHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration));
        if (ContentHost.RenderTransform is System.Windows.Media.TranslateTransform translate)
        {
            translate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, new DoubleAnimation(8, 0, duration));
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (System.Windows.Application.Current is App { IsExplicitShutdown: false })
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }
}
