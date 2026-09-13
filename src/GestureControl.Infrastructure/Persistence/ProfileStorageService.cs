using System.Text.Json;
using GestureControl.Core.Models;

namespace GestureControl.Infrastructure.Persistence;

/// <summary>
/// Persists application profiles and configuration to JSON on disk.
/// Section 10 of the technical architecture.
/// </summary>
public class ProfileStorageService
{
    private readonly string _profilesDirectory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ProfileStorageService(string? baseDir = null)
    {
        _profilesDirectory = baseDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles");
        Directory.CreateDirectory(_profilesDirectory);
    }

    public async Task SaveProfileAsync(AppProfile profile, CancellationToken ct = default)
    {
        string filePath = Path.Combine(_profilesDirectory, $"{profile.Id}.json");
        using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, profile, JsonOptions, ct);
    }

    public async Task<AppProfile?> LoadProfileAsync(string profileId, CancellationToken ct = default)
    {
        string filePath = Path.Combine(_profilesDirectory, $"{profileId}.json");
        if (!File.Exists(filePath))
            return null;

        using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<AppProfile>(stream, JsonOptions, ct);
    }

    public async Task<List<AppProfile>> LoadAllProfilesAsync(CancellationToken ct = default)
    {
        var list = new List<AppProfile>();
        foreach (var file in Directory.EnumerateFiles(_profilesDirectory, "*.json"))
        {
            try
            {
                using var stream = File.OpenRead(file);
                var profile = await JsonSerializer.DeserializeAsync<AppProfile>(stream, JsonOptions, ct);
                if (profile != null)
                {
                    list.Add(profile);
                }
            }
            catch
            {
                // Ignore corrupt individual file
            }
        }
        return list;
    }
}
