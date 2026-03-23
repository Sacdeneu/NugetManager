using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NugetManager.Services;
using NugetManager.Settings;
using WinForms = System.Windows.Forms;

namespace NugetManager.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly NuGetServerService _server;
    private readonly NuGetApiService _nugetApi;

    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isServerRunning;
    [ObservableProperty] private string _feedUrl = "";

    public AppSettings Settings => _settings;

    public SettingsViewModel(AppSettings settings, NuGetServerService server, NuGetApiService nugetApi)
    {
        _settings = settings;
        _server = server;
        _nugetApi = nugetApi;
        _feedUrl = nugetApi.FeedUrl;
        _isServerRunning = server.IsRunning;
    }

    [RelayCommand]
    public void BrowsePackagesFolder()
    {
        var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Dossier de stockage des packages",
            UseDescriptionForTitle = true,
            SelectedPath = string.IsNullOrEmpty(Settings.PackagesFolder)
                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                : Settings.PackagesFolder
        };

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            Settings.PackagesFolder = dialog.SelectedPath;
    }

    [RelayCommand]
    public async Task RestartServer()
    {
        StatusMessage = "Redémarrage du serveur...";
        await _server.StopAsync();
        await Task.Delay(500);
        await _server.StartAsync();
        IsServerRunning = _server.IsRunning;
        FeedUrl = _nugetApi.FeedUrl;
        StatusMessage = IsServerRunning ? "Serveur redémarré ✓" : "Erreur au démarrage";
    }

    [RelayCommand]
    public void CopyFeedUrl()
    {
        System.Windows.Clipboard.SetText(FeedUrl);
        StatusMessage = "URL copiée dans le presse-papiers ✓";
    }

    [RelayCommand]
    public async Task CheckServerStatus()
    {
        var ok = await _nugetApi.IsServerAvailableAsync();
        IsServerRunning = ok;
        StatusMessage = ok ? "Serveur en ligne ✓" : "Serveur hors ligne";
    }
}
