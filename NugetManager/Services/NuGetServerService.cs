using System.IO;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NugetManager.Settings;

namespace NugetManager.Services;

/// <summary>
/// Embedded NuGet v3 server using Kestrel — runs in-process with the WPF app.
/// Compatible with Visual Studio, dotnet CLI, and NuGet CLI.
/// </summary>
public class NuGetServerService
{
    private readonly AppSettings _settings;
    private readonly PackageStorageService _storage;
    private WebApplication? _app;

    public bool IsRunning { get; private set; }
    public string BaseUrl => $"http://localhost:{_settings.BaGetPort}";
    public string FeedUrl => $"{BaseUrl}/v3/index.json";

    public NuGetServerService(AppSettings settings, PackageStorageService storage)
    {
        _settings = settings;
        _storage = storage;
    }

    public async Task StartAsync()
    {
        if (IsRunning) return;

        var builder = WebApplication.CreateBuilder(
            [$"--urls=http://localhost:{_settings.BaGetPort}"]);

        builder.Logging.ClearProviders(); // Suppress console spam in WPF

        _app = builder.Build();

        MapRoutes(_app);

        await _app.StartAsync();
        IsRunning = true;

        RegisterInGlobalNuGetConfig();
    }

    public async Task StopAsync()
    {
        UnregisterFromGlobalNuGetConfig();

        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }
        IsRunning = false;
    }

    private static readonly string GlobalNuGetConfig = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NuGet", "NuGet.Config");

    private void RegisterInGlobalNuGetConfig()
    {
        try
        {
            var dir = Path.GetDirectoryName(GlobalNuGetConfig)!;
            Directory.CreateDirectory(dir);

            XDocument doc;
            if (File.Exists(GlobalNuGetConfig))
                doc = XDocument.Load(GlobalNuGetConfig);
            else
                doc = new XDocument(new XDeclaration("1.0", "utf-8", null),
                    new XElement("configuration", new XElement("packageSources")));

            var root = doc.Root!;
            var sources = root.Element("packageSources") ?? new XElement("packageSources");

            // Remove stale entry then re-add with current URL
            sources.Elements("add")
                .FirstOrDefault(e => e.Attribute("key")?.Value == "NugetManagerLocal")
                ?.Remove();

            sources.AddFirst(new XElement("add",
                new XAttribute("key", "NugetManagerLocal"),
                new XAttribute("value", FeedUrl),
                new XAttribute("allowInsecureConnections", "true")));

            if (root.Element("packageSources") == null)
                root.Add(sources);

            doc.Save(GlobalNuGetConfig);
        }
        catch { /* non-critical */ }
    }

    private void UnregisterFromGlobalNuGetConfig()
    {
        try
        {
            if (!File.Exists(GlobalNuGetConfig)) return;
            var doc = XDocument.Load(GlobalNuGetConfig);
            doc.Root?.Element("packageSources")
                ?.Elements("add")
                .FirstOrDefault(e => e.Attribute("key")?.Value == "NugetManagerLocal")
                ?.Remove();
            doc.Save(GlobalNuGetConfig);
        }
        catch { /* non-critical */ }
    }

    private void MapRoutes(WebApplication app)
    {
        var base_url = BaseUrl;

        // ── Service Index ──────────────────────────────────────────────────
        app.MapGet("/v3/index.json", () => Results.Json(new
        {
            version = "3.0.0",
            resources = new[]
            {
                new { @id = $"{base_url}/v3/search",             @type = "SearchQueryService" },
                new { @id = $"{base_url}/v3/search",             @type = "SearchQueryService/3.5.0" },
                new { @id = $"{base_url}/v3/flatcontainer/",     @type = "PackageBaseAddress/3.0.0" },
                new { @id = $"{base_url}/v3/registration/",      @type = "RegistrationsBaseUrl" },
                new { @id = $"{base_url}/v3/registration/",      @type = "RegistrationsBaseUrl/3.6.0" },
                new { @id = $"{base_url}/api/v2/package",        @type = "PackagePublish/2.0.0" },
            }
        }));

        // ── Search ─────────────────────────────────────────────────────────
        app.MapGet("/v3/search", (string? q, int? skip, int? take, bool? prerelease) =>
        {
            var results = _storage.Search(q ?? "", skip ?? 0, take ?? 20);

            var data = results.Select(p => new
            {
                id = p.Id,
                version = p.Version,
                description = p.Description ?? "",
                authors = p.Authors?.Split(',').Select(a => a.Trim()).ToArray() ?? [],
                tags = p.Tags.ToArray(),
                totalDownloads = p.TotalDownloads,
                projectUrl = p.ProjectUrl,
                versions = _storage.GetAllVersions(p.Id).Select(v => new
                {
                    version = v.Version,
                    downloads = v.TotalDownloads
                }).ToArray()
            });

            return Results.Json(new { totalHits = _storage.TotalCount, data });
        });

        // ── Flat Container: versions list ───────────────────────────────────
        app.MapGet("/v3/flatcontainer/{id}/index.json", (string id) =>
        {
            var versions = _storage.GetAllVersions(id).Select(p => p.Version).ToArray();
            if (versions.Length == 0) return Results.NotFound();
            return Results.Json(new { versions });
        });

        // ── Flat Container: download .nupkg ─────────────────────────────────
        app.MapGet("/v3/flatcontainer/{id}/{version}/{filename}", (string id, string version, string filename) =>
        {
            var path = _storage.GetPackagePath(id, version);
            if (path == null) return Results.NotFound();

            var stream = File.OpenRead(path);
            return Results.Stream(stream, "application/octet-stream", Path.GetFileName(path));
        });

        // ── Registration (package metadata for VS IntelliSense) ────────────
        app.MapGet("/v3/registration/{id}/index.json", (string id) =>
        {
            var packages = _storage.GetAllVersions(id);
            if (packages.Count == 0) return Results.NotFound();

            var items = packages.Select(p => new
            {
                catalogEntry = new
                {
                    id = p.Id,
                    version = p.Version,
                    description = p.Description ?? "",
                    authors = p.Authors ?? "",
                    tags = string.Join(" ", p.Tags),
                    projectUrl = p.ProjectUrl ?? "",
                    listed = p.IsListed,
                    published = p.Published.ToString("O"),
                },
                packageContent = $"{base_url}/v3/flatcontainer/{p.Id.ToLowerInvariant()}/{p.Version.ToLowerInvariant()}/{p.Id.ToLowerInvariant()}.{p.Version.ToLowerInvariant()}.nupkg",
                listed = p.IsListed,
            }).ToArray();

            return Results.Json(new
            {
                count = 1,
                items = new[]
                {
                    new
                    {
                        lower = packages.Last().Version,
                        upper = packages.First().Version,
                        count = packages.Count,
                        items
                    }
                }
            });
        });

        // ── Push (upload) ──────────────────────────────────────────────────
        app.MapPut("/api/v2/package", async (HttpRequest request) =>
        {
            try
            {
                var form = await request.ReadFormAsync();
                var file = form.Files.FirstOrDefault()
                    ?? form.Files["package"];

                if (file == null)
                    return Results.BadRequest("No package file found.");

                await using var stream = file.OpenReadStream();
                var (success, error) = await _storage.AddPackageAsync(stream, overwrite: true);

                return success ? Results.Created() : Results.Conflict(error);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });

        // ── Delete ─────────────────────────────────────────────────────────
        app.MapDelete("/api/v2/package/{id}/{version}", async (string id, string version) =>
        {
            var ok = await _storage.DeletePackageAsync(id, version);
            return ok ? Results.NoContent() : Results.NotFound();
        });
    }
}
