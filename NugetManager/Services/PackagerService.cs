using System.Diagnostics;
using System.IO;

namespace NugetManager.Services;

public class PackagerService
{
    private readonly NuGetApiService _nugetApi;

    public PackagerService(NuGetApiService nugetApi)
    {
        _nugetApi = nugetApi;
    }

    /// <summary>
    /// Runs dotnet pack on a .csproj or .sln file, streams output line by line.
    /// Returns true if the pack succeeded.
    /// </summary>
    public async Task<bool> PackAsync(
        string projectPath,
        string configuration,
        string? version,
        string? outputDir,
        bool includeSymbols,
        bool autoPublish,
        Action<string> onOutput,
        CancellationToken ct = default)
    {
        var args = $"pack \"{projectPath}\" -c {configuration}";

        if (!string.IsNullOrWhiteSpace(version))
            args += $" -p:PackageVersion={version}";

        var resolvedOutput = outputDir;
        if (string.IsNullOrWhiteSpace(resolvedOutput))
            resolvedOutput = Path.Combine(Path.GetDirectoryName(projectPath)!, "nupkg-output");

        args += $" -o \"{resolvedOutput}\"";

        if (includeSymbols)
            args += " --include-symbols --include-source";

        args += " --nologo";

        onOutput($"▶ dotnet {args}");
        onOutput("");

        var psi = new ProcessStartInfo("dotnet", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(projectPath)!
        };

        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) => { if (e.Data != null) onOutput(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) onOutput($"[err] {e.Data}"); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);

        var success = process.ExitCode == 0;

        if (success)
        {
            onOutput("");
            onOutput("✓ Pack réussi.");

            if (autoPublish)
            {
                var nupkgs = Directory.GetFiles(resolvedOutput, "*.nupkg")
                    .Where(f => !f.EndsWith(".snupkg")).ToArray();

                onOutput($"→ Publication de {nupkgs.Length} package(s)...");

                foreach (var nupkg in nupkgs)
                {
                    var (ok, error) = await _nugetApi.PushPackageAsync(nupkg);
                    onOutput(ok
                        ? $"  ✓ {Path.GetFileName(nupkg)} publié"
                        : $"  ✗ {Path.GetFileName(nupkg)} : {error}");
                }
            }
        }
        else
        {
            onOutput("");
            onOutput($"✗ Échec (code {process.ExitCode}).");
        }

        return success;
    }

    /// <summary>
    /// Lists all .csproj projects inside a .sln file.
    /// </summary>
    public static List<string> GetProjectsFromSolution(string slnPath)
    {
        var projects = new List<string>();
        var slnDir = Path.GetDirectoryName(slnPath)!;

        foreach (var line in File.ReadLines(slnPath))
        {
            // Project("{...}") = "Name", "relative\path.csproj", "{guid}"
            if (!line.TrimStart().StartsWith("Project(")) continue;
            var parts = line.Split(',');
            if (parts.Length < 2) continue;
            var rel = parts[1].Trim().Trim('"');
            if (!rel.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) continue;
            var full = Path.GetFullPath(Path.Combine(slnDir, rel));
            if (File.Exists(full))
                projects.Add(full);
        }

        return projects;
    }
}
