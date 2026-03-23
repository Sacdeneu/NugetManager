namespace NugetManager.Models;

public class TagData
{
    public Dictionary<string, List<string>> PackageTags { get; set; } = new();
}
