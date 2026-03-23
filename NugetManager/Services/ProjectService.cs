using System.IO;
using System.Xml.Linq;
using NugetManager.Models;
using NugetManager.Settings;

namespace NugetManager.Services;

/// <summary>
/// Scans .csproj files and manages NuGet feed sources via NuGet.Config.
/// </summary>
public class ProjectService
{
    private readonly AppSettings _settings;
    private readonly NuGetApiService _nugetApi;

    public ProjectService(AppSettings settings, NuGetApiService nugetApi)
    {
        _settings = settings;
        _nugetApi = nugetApi;
    }

    public async Task<List<ProjectInfo>> ScanFolderAsync(string folderPath)
    {
        var projects = new List<ProjectInfo>();

        var csprojFiles = Directory.GetFiles(folderPath, "*.csproj", SearchOption.AllDirectories);
        var localPackages = _nugetApi.SearchPackages(take: 500);

        // Build a map: id.lower → all versions (to find latest)
        var localIndex = localPackages
            .GroupBy(p => p.Id.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Version).First().Version);

        foreach (var path in csprojFiles)
        {
            try
            {
                var project = ParseProject(path, localIndex);
                project.IsLocalFeedActive = IsLocalFeedActive(project.FolderPath);
                projects.Add(project);
            }
            catch { /* skip malformed csproj */ }
        }

        return projects;
    }

    public void UpdatePackageVersion(ProjectInfo project, string packageId, string newVersion)
    {
        var doc = XDocument.Load(project.Path);
        var element = doc.Descendants("PackageReference")
            .FirstOrDefault(e => string.Equals(e.Attribute("Include")?.Value, packageId, StringComparison.OrdinalIgnoreCase));

        if (element == null) return;

        if (element.Attribute("Version") != null)
            element.SetAttributeValue("Version", newVersion);
        else
            element.Element("Version")?.SetValue(newVersion);

        doc.Save(project.Path);
    }

    public void EnableLocalFeed(ProjectInfo project)
    {
        var configPath = FindOrCreateNuGetConfig(project.FolderPath);
        var doc = File.Exists(configPath) ? XDocument.Load(configPath) : CreateDefaultConfig();

        var packageSources = doc.Root!.Element("packageSources")
            ?? new XElement("packageSources");

        // Remove existing local entry
        packageSources.Elements("add")
            .FirstOrDefault(e => e.Attribute("key")?.Value == "NugetManagerLocal")
            ?.Remove();

        packageSources.AddFirst(new XElement("add",
            new XAttribute("key", "NugetManagerLocal"),
            new XAttribute("value", _nugetApi.FeedUrl),
            new XAttribute("allowInsecureConnections", "true")));

        if (doc.Root.Element("packageSources") == null)
            doc.Root.Add(packageSources);

        doc.Save(configPath);
        project.IsLocalFeedActive = true;
        project.CurrentFeedSource = "local";
    }

    public void DisableLocalFeed(ProjectInfo project)
    {
        var configPath = FindOrCreateNuGetConfig(project.FolderPath);
        if (!File.Exists(configPath)) return;

        var doc = XDocument.Load(configPath);
        doc.Root?.Element("packageSources")
            ?.Elements("add")
            .FirstOrDefault(e => e.Attribute("key")?.Value == "NugetManagerLocal")
            ?.Remove();

        doc.Save(configPath);
        project.IsLocalFeedActive = false;
        project.CurrentFeedSource = "nuget.org";
    }

    public bool IsLocalFeedActive(string projectFolder)
    {
        var configPath = FindNuGetConfig(projectFolder);
        if (configPath == null) return false;

        try
        {
            var doc = XDocument.Load(configPath);
            return doc.Root?.Element("packageSources")
                ?.Elements("add")
                .Any(e => e.Attribute("key")?.Value == "NugetManagerLocal") ?? false;
        }
        catch { return false; }
    }

    private ProjectInfo ParseProject(string path, Dictionary<string, string> localIndex)
    {
        var doc = XDocument.Load(path);
        var refs = doc.Descendants("PackageReference")
            .Select(e =>
            {
                var id = e.Attribute("Include")?.Value ?? "";
                var ver = e.Attribute("Version")?.Value ?? e.Element("Version")?.Value ?? "";
                var idLower = id.ToLowerInvariant();
                var existsLocally = localIndex.ContainsKey(idLower);
                var latestLocal = existsLocally ? localIndex[idLower] : "";

                return new PackageReference
                {
                    Id = id,
                    Version = ver,
                    ExistsLocally = existsLocally,
                    LatestVersion = existsLocally ? latestLocal : ver,
                    LatestSource = existsLocally ? $"localhost:{_settings.BaGetPort}" : "nuget.org",
                };
            })
            .Where(r => !string.IsNullOrEmpty(r.Id))
            .ToList();

        return new ProjectInfo
        {
            Name = Path.GetFileNameWithoutExtension(path),
            Path = path,
            PackageReferences = refs,
        };
    }

    private string FindOrCreateNuGetConfig(string projectFolder)
    {
        // Walk up to find existing NuGet.Config, else create in solution root or project folder
        var existing = FindNuGetConfig(projectFolder);
        return existing ?? Path.Combine(projectFolder, "NuGet.Config");
    }

    private string? FindNuGetConfig(string startFolder)
    {
        var current = startFolder;
        for (int i = 0; i < 5; i++)
        {
            if (string.IsNullOrEmpty(current)) break;
            var candidate = Path.Combine(current, "NuGet.Config");
            if (File.Exists(candidate)) return candidate;
            current = Path.GetDirectoryName(current) ?? "";
        }
        return null;
    }

    private static XDocument CreateDefaultConfig()
    {
        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("configuration",
                new XElement("packageSources")));
    }
}
