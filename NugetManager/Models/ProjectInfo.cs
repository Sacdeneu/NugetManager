namespace NugetManager.Models;

public class ProjectInfo
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string FolderPath => System.IO.Path.GetDirectoryName(Path) ?? "";
    public List<PackageReference> PackageReferences { get; set; } = [];
    public string CurrentFeedSource { get; set; } = "nuget.org";
    public bool IsLocalFeedActive { get; set; }

    // Computed stats for left panel badges
    public int TotalPackages => PackageReferences.Count;
    public int UpdatesAvailable => PackageReferences.Count(p => !p.IsUpToDate);
    public int UpToDateCount => PackageReferences.Count(p => p.IsUpToDate);
    public string SourceLabel => IsLocalFeedActive ? "local" : "nuget.org";
}

public class PackageReference
{
    public string Id { get; set; } = "";
    public string Version { get; set; } = "";
    public bool ExistsLocally { get; set; }

    // Populated by ProjectService from local index
    public string LatestVersion { get; set; } = "";
    public string LatestSource { get; set; } = "nuget.org";

    public bool IsUpToDate => string.IsNullOrEmpty(LatestVersion) || LatestVersion == Version;
    public bool NeedsUpdate => !IsUpToDate;

    // Source where current version is resolved from
    public string Source => ExistsLocally ? "localhost" : "nuget.org";
}
