using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;
using NugetManager.ViewModels;

namespace NugetManager.Views;

public partial class SettingsView : UserControl
{
    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
