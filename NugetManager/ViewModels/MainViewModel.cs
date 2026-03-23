using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using NugetManager.Settings;
using NugetManager.Views;
using BeyondWPF.Core.Abstractions;
using BeyondWPF.Core.Enums;

namespace NugetManager.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IThemeService _themeService;
    private readonly IDialogService _dialogService;
    private readonly IServiceProvider _serviceProvider;
    private readonly AppSettings _settings;

    [ObservableProperty] private string _title = "NuGet Manager";
    [ObservableProperty] private object? _currentView;
    [ObservableProperty] private bool _isDarkTheme = true;
    [ObservableProperty] private System.Windows.CornerRadius _borderCornerRadius;
    [ObservableProperty] private string _activeSection = "Packages";

    public AppSettings Settings => _settings;
    public object? DialogContent => _dialogService.CurrentView;
    public bool IsDialogOpen => _dialogService.IsOpen;

    public MainViewModel(
        IThemeService themeService,
        IDialogService dialogService,
        IServiceProvider serviceProvider,
        AppSettings settings)
    {
        _themeService = themeService;
        _dialogService = dialogService;
        _serviceProvider = serviceProvider;
        _settings = settings;

        _dialogService.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(IDialogService.CurrentView))
                OnPropertyChanged(nameof(DialogContent));
            else if (e.PropertyName == nameof(IDialogService.IsOpen))
                OnPropertyChanged(nameof(IsDialogOpen));
        };

        if (Enum.TryParse<SystemTheme>(_settings.Theme, out var savedTheme))
        {
            _themeService.ApplyTheme(savedTheme);
            _isDarkTheme = savedTheme == SystemTheme.Dark;
        }

        UpdateCornerRadius();
        _themeService.ApplySystemAccent(_settings.UseAccentColor);

        NavigateToPackages();
    }

    private void UpdateCornerRadius()
    {
        BorderCornerRadius = _settings.IsRoundedCorners
            ? new System.Windows.CornerRadius(10)
            : new System.Windows.CornerRadius(0);
    }

    [RelayCommand]
    public void ToggleTheme()
    {
        if (IsDarkTheme)
        {
            _themeService.ApplyTheme(SystemTheme.Light);
            _settings.Theme = SystemTheme.Light.ToString();
            IsDarkTheme = false;
        }
        else
        {
            _themeService.ApplyTheme(SystemTheme.Dark);
            _settings.Theme = SystemTheme.Dark.ToString();
            IsDarkTheme = true;
        }
        _themeService.ApplySystemAccent(_settings.UseAccentColor);
    }

    [RelayCommand]
    public void NavigateToPackages()
    {
        ActiveSection = "Packages";
        var view = _serviceProvider.GetRequiredService<PackagesView>();
        CurrentView = view;
        _ = view.ViewModel.InitializeAsync();
    }

    [RelayCommand]
    public void NavigateToProjects()
    {
        ActiveSection = "Projects";
        CurrentView = _serviceProvider.GetRequiredService<ProjectsView>();
    }

    [RelayCommand]
    public void NavigateToPackager()
    {
        ActiveSection = "Packager";
        CurrentView = _serviceProvider.GetRequiredService<PackagerView>();
    }

    [RelayCommand]
    public void NavigateToSettings()
    {
        ActiveSection = "Settings";
        CurrentView = _serviceProvider.GetRequiredService<SettingsView>();
    }
}
