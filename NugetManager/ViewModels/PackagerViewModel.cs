using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NugetManager.Services;
using WinForms = System.Windows.Forms;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace NugetManager.ViewModels;

public partial class PackagerViewModel : ObservableObject
{
    private readonly PackagerService _packager;

    // ── .csproj tab ───────────────────────────────────────────────────────
    [ObservableProperty] private string _csprojPath = "";
    [ObservableProperty] private string _csprojVersion = "";
    [ObservableProperty] private string _csprojOutputDir = "";
    [ObservableProperty] private string _csprojConfiguration = "Release";
    [ObservableProperty] private bool _csprojAutoPublish = true;
    [ObservableProperty] private bool _csprojIncludeSymbols;

    // ── .sln tab ──────────────────────────────────────────────────────────
    [ObservableProperty] private string _slnPath = "";
    [ObservableProperty] private string _slnConfiguration = "Release";
    [ObservableProperty] private bool _slnAutoPublish = true;
    [ObservableProperty] private ObservableCollection<SlnProjectItem> _slnProjects = [];

    // ── shared ────────────────────────────────────────────────────────────
    [ObservableProperty] private string _consoleOutput = "En attente…";
    [ObservableProperty] private bool _isPacking;
    [ObservableProperty] private string _activeTab = "csproj"; // csproj | sln | upload

    private CancellationTokenSource? _cts;

    public PackagerViewModel(PackagerService packager)
    {
        _packager = packager;
    }

    // ── Tab ────────────────────────────────────────────────────────────────
    [RelayCommand]
    public void SwitchTab(string tab) => ActiveTab = tab;

    // ── .csproj browse ────────────────────────────────────────────────────
    [RelayCommand]
    public void BrowseCsproj()
    {
        var dlg = new OpenFileDialog { Filter = "Projet C# (*.csproj)|*.csproj", Title = "Sélectionner un .csproj" };
        if (dlg.ShowDialog() == true)
            CsprojPath = dlg.FileName;
    }

    [RelayCommand]
    public void BrowseCsprojOutput()
    {
        var dlg = new WinForms.FolderBrowserDialog { Description = "Dossier de sortie", UseDescriptionForTitle = true };
        if (dlg.ShowDialog() == WinForms.DialogResult.OK)
            CsprojOutputDir = dlg.SelectedPath;
    }

    // ── .sln browse ───────────────────────────────────────────────────────
    [RelayCommand]
    public void BrowseSln()
    {
        var dlg = new OpenFileDialog { Filter = "Solution Visual Studio (*.sln)|*.sln", Title = "Sélectionner un .sln" };
        if (dlg.ShowDialog() != true) return;
        SlnPath = dlg.FileName;
        LoadSlnProjects();
    }

    private void LoadSlnProjects()
    {
        SlnProjects.Clear();
        try
        {
            var projects = PackagerService.GetProjectsFromSolution(SlnPath);
            foreach (var p in projects)
                SlnProjects.Add(new SlnProjectItem(p));
        }
        catch { }
    }

    // ── Pack .csproj ──────────────────────────────────────────────────────
    [RelayCommand(CanExecute = nameof(CanPack))]
    public async Task PackCsproj()
    {
        if (string.IsNullOrWhiteSpace(CsprojPath)) return;
        await RunPackAsync(CsprojPath, CsprojConfiguration, CsprojVersion, CsprojOutputDir, CsprojIncludeSymbols, CsprojAutoPublish);
    }

    private bool CanPack() => !IsPacking;

    // ── Pack .sln (selected projects) ─────────────────────────────────────
    [RelayCommand(CanExecute = nameof(CanPack))]
    public async Task PackSln()
    {
        var selected = SlnProjects.Where(p => p.IsSelected).ToList();
        if (!selected.Any()) return;

        AppendLine("▶ Packing de la solution…");
        AppendLine("");

        foreach (var proj in selected)
            await RunPackAsync(proj.Path, SlnConfiguration, "", "", false, SlnAutoPublish);
    }

    private async Task RunPackAsync(string path, string config, string version, string output, bool symbols, bool publish)
    {
        IsPacking = true;
        PackCsprojCommand.NotifyCanExecuteChanged();
        PackSlnCommand.NotifyCanExecuteChanged();
        _cts = new CancellationTokenSource();
        ConsoleOutput = "";

        try
        {
            await _packager.PackAsync(path, config, version, output, symbols, publish, AppendLine, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            AppendLine("⊘ Opération annulée.");
        }
        finally
        {
            IsPacking = false;
            PackCsprojCommand.NotifyCanExecuteChanged();
            PackSlnCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    public void CancelPack()
    {
        _cts?.Cancel();
    }

    [RelayCommand]
    public void ClearConsole() => ConsoleOutput = "En attente…";

    private void AppendLine(string line)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (ConsoleOutput == "En attente…")
                ConsoleOutput = line;
            else
                ConsoleOutput += "\n" + line;
        });
    }
}

public partial class SlnProjectItem : ObservableObject
{
    [ObservableProperty] private bool _isSelected = true;

    public string Path { get; }
    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);

    public SlnProjectItem(string path) => Path = path;
}
