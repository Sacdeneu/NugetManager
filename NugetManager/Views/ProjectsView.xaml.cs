using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;
using NugetManager.ViewModels;

namespace NugetManager.Views;

public partial class ProjectsView : UserControl
{
    public ProjectsView(ProjectsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
