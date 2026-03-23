using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using NugetManager.Models;
using NugetManager.Settings;

namespace NugetManager.Services;

/// <summary>
/// UI-facing service for package operations.
/// Uses PackageStorageService directly for reads/writes,
/// and calls our own Kestrel server for upload (to go through the NuGet protocol).
/// </summary>
public class NuGetApiService
{
    private readonly AppSettings _settings;
    private readonly PackageStorageService _storage;
    private readonly TagService _tagService;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public string FeedUrl => $"http://localhost:{_settings.BaGetPort}/v3/index.json";

    public NuGetApiService(AppSettings settings, PackageStorageService storage, TagService tagService)
    {
        _settings = settings;
        _storage = storage;
        _tagService = tagService;
    }

    public List<PackageInfo> SearchPackages(string query = "", int skip = 0, int take = 50)
    {
        var results = _storage.Search(query, skip, take);
        // Inject custom tags
        foreach (var p in results)
            p.CustomTags = _tagService.GetTags($"{p.Id}@{p.Version}");
        return results;
    }

    public async Task<(bool Success, string? Error)> PushPackageAsync(string nupkgPath)
    {
        try
        {
            await using var fs = File.OpenRead(nupkgPath);
            return await _storage.AddPackageAsync(fs, overwrite: true);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<bool> DeletePackageAsync(string id, string version)
    {
        return await _storage.DeletePackageAsync(id, version);
    }

    public async Task<bool> IsServerAvailableAsync()
    {
        try
        {
            var response = await _http.GetAsync(FeedUrl);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
