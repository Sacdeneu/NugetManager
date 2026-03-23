using System.IO;
using System.Text.Json;
using NugetManager.Models;

namespace NugetManager.Services;

/// <summary>
/// Stores custom tags for packages in a local JSON file.
/// Key format: "PackageId@Version"
/// </summary>
public class TagService
{
    private readonly string _filePath;
    private TagData _data = new();

    public TagService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NugetManager");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "tags.json");
        Load();
    }

    public List<string> GetTags(string packageKey) =>
        _data.PackageTags.TryGetValue(packageKey, out var tags) ? tags : [];

    public void SetTags(string packageKey, List<string> tags)
    {
        if (tags.Count == 0)
            _data.PackageTags.Remove(packageKey);
        else
            _data.PackageTags[packageKey] = tags;
        Save();
    }

    public void AddTag(string packageKey, string tag)
    {
        if (!_data.PackageTags.TryGetValue(packageKey, out var tags))
        {
            tags = [];
            _data.PackageTags[packageKey] = tags;
        }
        if (!tags.Contains(tag))
        {
            tags.Add(tag);
            Save();
        }
    }

    public void RemoveTag(string packageKey, string tag)
    {
        if (_data.PackageTags.TryGetValue(packageKey, out var tags))
        {
            tags.Remove(tag);
            if (tags.Count == 0)
                _data.PackageTags.Remove(packageKey);
            Save();
        }
    }

    public List<string> GetAllUniqueTags()
    {
        return _data.PackageTags.Values
            .SelectMany(t => t)
            .Distinct()
            .OrderBy(t => t)
            .ToList();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                _data = JsonSerializer.Deserialize<TagData>(json) ?? new TagData();
            }
        }
        catch { _data = new TagData(); }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch { /* best effort */ }
    }
}
