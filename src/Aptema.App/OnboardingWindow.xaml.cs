using System.Windows;
using Aptema.App.ViewModels;

namespace Aptema.App;

public partial class OnboardingWindow : Window
{
    public OnboardingWindow(OnboardingViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not OnboardingViewModel model) return;
        if (model.IsLastStep)
        {
            DialogResult = true;
            return;
        }
        model.MoveNext();
    }
}
