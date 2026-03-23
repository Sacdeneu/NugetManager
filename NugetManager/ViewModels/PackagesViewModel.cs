using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NugetManager.Models;
using NugetManager.Services;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace NugetManager.ViewModels;

public partial class PackagesViewModel : ObservableObject
{
    private readonly NuGetApiService _nugetApi;
    private readonly TagService _tagService;
    private readonly PackageStorageService _storage;

    [ObservableProperty] private ObservableCollection<PackageInfo> _packages = [];
    [ObservableProperty] private PackageInfo? _selectedPackage;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isServerAvailable;
    [ObservableProperty] private string _newTag = "";
    [ObservableProperty] private string _filterTag = "";
    [ObservableProperty] private ObservableCollection<string> _allTags = [];

    // Stats
    [ObservableProperty] private int _uniquePackagesCount;
    [ObservableProperty] private int _totalVersionsCount;
    [ObservableProperty] private string _totalSizeFormatted = "—";
    [ObservableProperty] private int _serverPort;

    public PackagesViewModel(NuGetApiService nugetApi, TagService tagService, PackageStorageService storage, Settings.AppSettings settings)
    {
        _nugetApi = nugetApi;
        _tagService = tagService;
        _storage = storage;
        _serverPort = settings.BaGetPort;
    }

    public async Task InitializeAsync()
    {
        IsServerAvailable = await _nugetApi.IsServerAvailableAsync();
        RefreshAllTags();
        LoadPackages();
    }

    [RelayCommand]
    public void SelectPackage(PackageInfo? package)
    {
        SelectedPackage = package;
    }

    [RelayCommand]
    public void LoadPackages()
    {
        IsLoading = true;
        StatusMessage = "";
        try
        {
            var result = _nugetApi.SearchPackages(SearchText);
            Packages = new ObservableCollection<PackageInfo>(result);
            IsServerAvailable = true;
            StatusMessage = $"{Packages.Count} package(s)";
            UniquePackagesCount = _storage.UniquePackagesCount;
            TotalVersionsCount = _storage.TotalCount;
            TotalSizeFormatted = _storage.TotalSizeFormatted();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur : {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task UploadPackage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Sélectionner un package NuGet",
            Filter = "NuGet Package (*.nupkg)|*.nupkg",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true) return;

        IsLoading = true;
        int success = 0, fail = 0;

        foreach (var file in dialog.FileNames)
        {
            var (ok, _) = await _nugetApi.PushPackageAsync(file);
            if (ok) success++; else fail++;
        }

        StatusMessage = $"Upload : {success} réussi(s), {fail} échoué(s)";
        IsLoading = false;
        LoadPackages();
    }

    [RelayCommand]
    public async Task DeletePackage(PackageInfo? package)
    {
        package ??= SelectedPackage;
        if (package == null) return;

        var result = MessageBox.Show(
            $"Supprimer {package.DisplayName} ?",
            "Confirmer la suppression",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        IsLoading = true;
        var ok = await _nugetApi.DeletePackageAsync(package.Id, package.Version);
        StatusMessage = ok ? $"{package.DisplayName} supprimé" : "Échec de la suppression";
        IsLoading = false;

        if (ok) LoadPackages();
    }

    [RelayCommand]
    public void AddTag()
    {
        if (SelectedPackage == null || string.IsNullOrWhiteSpace(NewTag)) return;

        var key = $"{SelectedPackage.Id}@{SelectedPackage.Version}";
        _tagService.AddTag(key, NewTag.Trim());
        SelectedPackage.CustomTags = _tagService.GetTags(key);
        OnPropertyChanged(nameof(SelectedPackage));
        NewTag = "";
        RefreshAllTags();

        var idx = Packages.IndexOf(SelectedPackage);
        if (idx >= 0)
        {
            Packages.RemoveAt(idx);
            Packages.Insert(idx, SelectedPackage);
        }
    }

    [RelayCommand]
    public void RemoveTag(string tag)
    {
        if (SelectedPackage == null) return;

        var key = $"{SelectedPackage.Id}@{SelectedPackage.Version}";
        _tagService.RemoveTag(key, tag);
        SelectedPackage.CustomTags = _tagService.GetTags(key);
        OnPropertyChanged(nameof(SelectedPackage));
        RefreshAllTags();
    }

    [RelayCommand]
    public void FilterByTag()
    {
        IsLoading = true;
        var all = _nugetApi.SearchPackages(take: 500);
        var filtered = string.IsNullOrEmpty(FilterTag)
            ? all
            : all.Where(p => p.CustomTags.Contains(FilterTag)).ToList();
        Packages = new ObservableCollection<PackageInfo>(filtered);
        StatusMessage = string.IsNullOrEmpty(FilterTag)
            ? $"{Packages.Count} package(s)"
            : $"{Packages.Count} package(s) avec le tag '{FilterTag}'";
        IsLoading = false;
    }

    [RelayCommand]
    public void ClearFilter()
    {
        FilterTag = "";
        LoadPackages();
    }

    private void RefreshAllTags()
    {
        AllTags = new ObservableCollection<string>(_tagService.GetAllUniqueTags());
    }
}
