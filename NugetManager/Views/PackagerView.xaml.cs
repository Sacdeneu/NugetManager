using NugetManager.Services;
using NugetManager.ViewModels;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using UserControl = System.Windows.Controls.UserControl;
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using RoutedEventArgs = System.Windows.RoutedEventArgs;

namespace NugetManager.Views;

public partial class PackagerView : UserControl
{
    private readonly NuGetApiService _nugetApi;

    public PackagerViewModel ViewModel => (PackagerViewModel)DataContext;

    public PackagerView(PackagerViewModel viewModel, NuGetApiService nugetApi)
    {
        _nugetApi = nugetApi;
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        foreach (var f in files.Where(f => f.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase)))
        {
            var (ok, error) = await _nugetApi.PushPackageAsync(f);
            MessageBox.Show(ok ? $"✓ {System.IO.Path.GetFileName(f)} uploadé" : $"✗ {error}",
                "Upload", MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
    }

    private async void ChooseUploadFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "NuGet Package (*.nupkg)|*.nupkg", Multiselect = true };
        if (dlg.ShowDialog() != true) return;
        foreach (var f in dlg.FileNames)
        {
            var (ok, error) = await _nugetApi.PushPackageAsync(f);
            MessageBox.Show(ok ? $"✓ {System.IO.Path.GetFileName(f)} uploadé" : $"✗ {error}",
                "Upload", MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
    }
}
