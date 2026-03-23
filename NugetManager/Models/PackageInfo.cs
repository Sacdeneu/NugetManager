namespace NugetManager.Models;

public class PackageInfo
{
    public string Id { get; set; } = "";
    public string Version { get; set; } = "";
    public string? Description { get; set; }
    public string? Authors { get; set; }
    public long TotalDownloads { get; set; }
    public DateTime Published { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<string> CustomTags { get; set; } = [];
    public bool IsListed { get; set; } = true;
    public string? ProjectUrl { get; set; }
    public string? IconUrl { get; set; }

    // Computed for UI
    public string DisplayName => $"{Id} {Version}";
    public string AuthorsDisplay => Authors ?? "—";
    public string TagsDisplay => CustomTags.Count > 0 ? string.Join(", ", CustomTags) : "";
    public string PublishedFormatted => Published == default ? "—" : Published.ToString("yyyy-MM-dd");
    public bool IsPreview => Version.Contains('-');

    // Populated from disk after index build
    public long SizeBytes { get; set; }
    public string SizeFormatted => SizeBytes switch
    {
        >= 1_048_576 => $"{SizeBytes / 1_048_576.0:F1} MB",
        >= 1024 => $"{SizeBytes / 1024.0:F0} KB",
        _ => $"{SizeBytes} B"
    };
}
