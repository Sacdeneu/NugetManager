using System.IO;
using System.Text.Json;
using NuGet.Packaging;
using NugetManager.Models;
using NugetManager.Settings;

namespace NugetManager.Services;

/// <summary>
/// Manages .nupkg files on disk and maintains an in-memory index.
/// Storage layout: {Root}/{id.lower}/{version}/{id.lower}.{version}.nupkg
/// </summary>
public class PackageStorageService
{
    private readonly AppSettings _settings;
    private readonly List<PackageInfo> _index = [];
    private readonly SemaphoreSlim _lock = new(1, 1);

    public string StorageRoot => string.IsNullOrEmpty(_settings.PackagesFolder)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NugetManager", "packages")
        : _settings.PackagesFolder;

    public PackageStorageService(AppSettings settings)
    {
        _settings = settings;
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(StorageRoot);
        await RebuildIndexAsync();
    }

    public List<PackageInfo> Search(string query = "", int skip = 0, int take = 50)
    {
        IEnumerable<PackageInfo> results = _index;

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.ToLowerInvariant();
            results = results.Where(p =>
                p.Id.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (p.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.Authors?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        return results.Skip(skip).Take(take).ToList();
    }

    public List<PackageInfo> GetAllVersions(string id)
    {
        return _index
            .Where(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.Version)
            .ToList();
    }

    public PackageInfo? GetPackage(string id, string version)
    {
        return _index.FirstOrDefault(p =>
            p.Id.Equals(id, StringComparison.OrdinalIgnoreCase) &&
            p.Version.Equals(version, StringComparison.OrdinalIgnoreCase));
    }

    public string? GetPackagePath(string id, string version)
    {
        var idLower = id.ToLowerInvariant();
        var versionLower = version.ToLowerInvariant();
        var path = Path.Combine(StorageRoot, idLower, versionLower, $"{idLower}.{versionLower}.nupkg");
        return File.Exists(path) ? path : null;
    }

    public async Task<(bool Success, string? Error)> AddPackageAsync(Stream nupkgStream, bool overwrite = false)
    {
        await _lock.WaitAsync();
        try
        {
            // Read metadata from the .nupkg stream
            using var ms = new MemoryStream();
            await nupkgStream.CopyToAsync(ms);
            ms.Position = 0;

            PackageInfo info;
            try
            {
                info = ReadMetadata(ms);
            }
            catch (Exception ex)
            {
                return (false, $"Impossible de lire les métadonnées : {ex.Message}");
            }

            var idLower = info.Id.ToLowerInvariant();
            var versionLower = info.Version.ToLowerInvariant();
            var dir = Path.Combine(StorageRoot, idLower, versionLower);
            var dest = Path.Combine(dir, $"{idLower}.{versionLower}.nupkg");

            if (File.Exists(dest) && !overwrite)
                return (false, $"{info.Id} {info.Version} existe déjà.");

            Directory.CreateDirectory(dir);
            ms.Position = 0;
            await using var fs = File.Create(dest);
            await ms.CopyToAsync(fs);

            // Update index
            _index.RemoveAll(p =>
                p.Id.Equals(info.Id, StringComparison.OrdinalIgnoreCase) &&
                p.Version.Equals(info.Version, StringComparison.OrdinalIgnoreCase));
            _index.Add(info);

            return (true, null);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeletePackageAsync(string id, string version)
    {
        await _lock.WaitAsync();
        try
        {
            var path = GetPackagePath(id, version);
            if (path == null) return false;

            File.Delete(path);

            // Remove empty dirs
            var dir = Path.GetDirectoryName(path)!;
            if (!Directory.EnumerateFiles(dir).Any())
            {
                Directory.Delete(dir);
                var parentDir = Path.GetDirectoryName(dir)!;
                if (!Directory.EnumerateFileSystemEntries(parentDir).Any())
                    Directory.Delete(parentDir);
            }

            _index.RemoveAll(p =>
                p.Id.Equals(id, StringComparison.OrdinalIgnoreCase) &&
                p.Version.Equals(version, StringComparison.OrdinalIgnoreCase));

            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public int TotalCount => _index.Count;
    public int UniquePackagesCount => _index.Select(p => p.Id.ToLowerInvariant()).Distinct().Count();

    public string TotalSizeFormatted()
    {
        long total = 0;
        foreach (var nupkg in Directory.EnumerateFiles(StorageRoot, "*.nupkg", SearchOption.AllDirectories))
        {
            try { total += new FileInfo(nupkg).Length; } catch { }
        }
        return total switch
        {
            >= 1_073_741_824 => $"{total / 1_073_741_824.0:F1} GB",
            >= 1_048_576 => $"{total / 1_048_576.0:F1} MB",
            >= 1024 => $"{total / 1024.0:F0} KB",
            _ => $"{total} B"
        };
    }

    private async Task RebuildIndexAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _index.Clear();

            foreach (var nupkg in Directory.EnumerateFiles(StorageRoot, "*.nupkg", SearchOption.AllDirectories))
            {
                try
                {
                    await using var fs = File.OpenRead(nupkg);
                    var info = ReadMetadata(fs);
                    _index.Add(info);
                }
                catch { /* skip corrupt packages */ }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private static PackageInfo ReadMetadata(Stream stream)
    {
        using var reader = new PackageArchiveReader(stream, leaveStreamOpen: true);
        using var nuspecStream = reader.GetNuspec();
        var nuspec = new NuspecReader(nuspecStream);

        return new PackageInfo
        {
            Id = nuspec.GetId(),
            Version = nuspec.GetVersion().ToNormalizedString(),
            Description = nuspec.GetDescription(),
            Authors = nuspec.GetAuthors(),
            Tags = nuspec.GetTags()?.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList() ?? [],
            Published = DateTime.UtcNow,
            IsListed = true,
            ProjectUrl = nuspec.GetProjectUrl(),
        };
    }
}
