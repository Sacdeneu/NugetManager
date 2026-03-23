using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NugetManager.Models;
using NugetManager.Services;
using NugetManager.Settings;
using WinForms = System.Windows.Forms;

namespace NugetManager.ViewModels;

public partial class ProjectsViewModel : ObservableObject
{
    private readonly ProjectService _projectService;
    private readonly AppSettings _settings;

    [ObservableProperty] private ObservableCollection<ProjectInfo> _projects = [];
    [ObservableProperty] private ProjectInfo? _selectedProject;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _scannedFolder = "";
    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private ObservableCollection<PackageReference> _filteredPackages = [];

    // Stats computed from SelectedProject
    public int SelectedTotalPackages => SelectedProject?.TotalPackages ?? 0;
    public int SelectedUpToDateCount => SelectedProject?.UpToDateCount ?? 0;
    public int SelectedUpdatesCount => SelectedProject?.UpdatesAvailable ?? 0;
    public string SelectedActiveSource => SelectedProject?.IsLocalFeedActive == true
        ? $"localhost:{_settings.BaGetPort}"
        : "nuget.org";
    public bool HasUpdates => SelectedUpdatesCount > 0;

    public ProjectsViewModel(ProjectService projectService, AppSettings settings)
    {
        _projectService = projectService;
        _settings = settings;
        _scannedFolder = settings.LastProjectFolder;
    }

    partial void OnSelectedProjectChanged(ProjectInfo? value)
    {
        UpdateFilteredPackages();
        OnPropertyChanged(nameof(SelectedTotalPackages));
        OnPropertyChanged(nameof(SelectedUpToDateCount));
        OnPropertyChanged(nameof(SelectedUpdatesCount));
        OnPropertyChanged(nameof(SelectedActiveSource));
        OnPropertyChanged(nameof(HasUpdates));
    }

    partial void OnFilterTextChanged(string value) => UpdateFilteredPackages();

    private void UpdateFilteredPackages()
    {
        if (SelectedProject == null) { FilteredPackages = []; return; }

        var q = FilterText.ToLowerInvariant();
        var items = SelectedProject.PackageReferences
            .Where(p => string.IsNullOrEmpty(q) || p.Id.Contains(q, StringComparison.OrdinalIgnoreCase));

        FilteredPackages = new ObservableCollection<PackageReference>(items);
    }

    [RelayCommand]
    public void BrowseFolder()
    {
        var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Sélectionner un dossier de projets",
            UseDescriptionForTitle = true,
            SelectedPath = string.IsNullOrEmpty(ScannedFolder)
                ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                : ScannedFolder
        };
        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            ScannedFolder = dialog.SelectedPath;
            _settings.LastProjectFolder = ScannedFolder;
            _ = ScanFolderCommand.ExecuteAsync(null);
        }
    }

    [RelayCommand]
    public async Task ScanFolder()
    {
        if (string.IsNullOrEmpty(ScannedFolder)) { BrowseFolder(); return; }

        IsLoading = true;
        StatusMessage = "Scan en cours...";
        try
        {
            var results = await _projectService.ScanFolderAsync(ScannedFolder);
            Projects = new ObservableCollection<ProjectInfo>(results);
            StatusMessage = $"{Projects.Count} projet(s) trouvé(s)";
        }
        catch (Exception ex) { StatusMessage = $"Erreur : {ex.Message}"; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public void SelectProject(ProjectInfo? project) => SelectedProject = project;

    [RelayCommand]
    public void EnableLocalFeed(ProjectInfo? project)
    {
        project ??= SelectedProject;
        if (project == null) return;
        try
        {
            _projectService.EnableLocalFeed(project);
            OnPropertyChanged(nameof(SelectedActiveSource));
            StatusMessage = $"Feed local activé pour {project.Name}";
        }
        catch (Exception ex) { StatusMessage = $"Erreur : {ex.Message}"; }
    }

    [RelayCommand]
    public void DisableLocalFeed(ProjectInfo? project)
    {
        project ??= SelectedProject;
        if (project == null) return;
        try
        {
            _projectService.DisableLocalFeed(project);
            OnPropertyChanged(nameof(SelectedActiveSource));
            StatusMessage = $"Feed local désactivé pour {project.Name}";
        }
        catch (Exception ex) { StatusMessage = $"Erreur : {ex.Message}"; }
    }

    [RelayCommand]
    public void ChangeSource()
    {
        if (SelectedProject == null) return;
        if (SelectedProject.IsLocalFeedActive)
            DisableLocalFeed(SelectedProject);
        else
            EnableLocalFeed(SelectedProject);
    }

    [RelayCommand]
    public void UpdatePackage(PackageReference? pkg)
    {
        if (SelectedProject == null || pkg == null || pkg.IsUpToDate) return;
        try
        {
            _projectService.UpdatePackageVersion(SelectedProject, pkg.Id, pkg.LatestVersion);
            pkg.Version = pkg.LatestVersion;
            UpdateFilteredPackages();
            OnPropertyChanged(nameof(SelectedUpToDateCount));
            OnPropertyChanged(nameof(SelectedUpdatesCount));
            OnPropertyChanged(nameof(HasUpdates));
            StatusMessage = $"{pkg.Id} mis à jour vers {pkg.LatestVersion}";
        }
        catch (Exception ex) { StatusMessage = $"Erreur : {ex.Message}"; }
    }

    [RelayCommand]
    public void UpdateAll()
    {
        if (SelectedProject == null) return;
        var toUpdate = SelectedProject.PackageReferences.Where(p => p.NeedsUpdate).ToList();
        foreach (var pkg in toUpdate)
            UpdatePackage(pkg);
        StatusMessage = $"{toUpdate.Count} package(s) mis à jour";
    }

    [RelayCommand]
    public void EnableAllLocalFeeds()
    {
        foreach (var project in Projects)
            _projectService.EnableLocalFeed(project);
        StatusMessage = $"Feed local activé pour {Projects.Count} projet(s)";
        OnPropertyChanged(nameof(Projects));
    }

    [RelayCommand]
    public void DisableAllLocalFeeds()
    {
        foreach (var project in Projects)
            _projectService.DisableLocalFeed(project);
        StatusMessage = $"Feed local désactivé pour {Projects.Count} projet(s)";
        OnPropertyChanged(nameof(Projects));
    }

    [RelayCommand]
    public void OpenProjectFolder(ProjectInfo? project)
    {
        project ??= SelectedProject;
        if (project == null) return;
        System.Diagnostics.Process.Start("explorer.exe", project.FolderPath);
    }
}
