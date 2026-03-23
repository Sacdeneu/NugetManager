using System.Windows;
using NugetManager.ViewModels;

namespace NugetManager;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        SourceInitialized += MainWindow_SourceInitialized;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        if (!_viewModel.Settings.SaveWindowPosition) return;

        WindowStartupLocation = WindowStartupLocation.Manual;
        Top = _viewModel.Settings.WindowTop;
        Left = _viewModel.Settings.WindowLeft;
        Width = _viewModel.Settings.WindowWidth;
        Height = _viewModel.Settings.WindowHeight;

        if (Enum.TryParse<WindowState>(_viewModel.Settings.WindowState, out var state))
            WindowState = state;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_viewModel.Settings.SaveWindowPosition) return;

        _viewModel.Settings.WindowState = WindowState.ToString();
        if (WindowState == WindowState.Normal)
        {
            _viewModel.Settings.WindowTop = Top;
            _viewModel.Settings.WindowLeft = Left;
            _viewModel.Settings.WindowWidth = Width;
            _viewModel.Settings.WindowHeight = Height;
        }
        else
        {
            _viewModel.Settings.WindowTop = RestoreBounds.Top;
            _viewModel.Settings.WindowLeft = RestoreBounds.Left;
            _viewModel.Settings.WindowWidth = RestoreBounds.Width;
            _viewModel.Settings.WindowHeight = RestoreBounds.Height;
        }
        _viewModel.Settings.SaveSettings();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void DialogOverlay_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        e.Handled = true;
}
