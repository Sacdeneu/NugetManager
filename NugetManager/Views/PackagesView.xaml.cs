using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;
using NugetManager.ViewModels;

namespace NugetManager.Views;

public partial class PackagesView : UserControl
{
    public PackagesViewModel ViewModel { get; }

    public PackagesView(PackagesViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }
}
