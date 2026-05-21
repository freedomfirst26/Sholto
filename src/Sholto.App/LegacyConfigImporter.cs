using System.Text.Json;
using Sholto.Storage;

namespace Sholto.App;

/// <summary>One-shot bridge from the old ~/.config/sholto/config.json file
/// (used before settings moved into SQLite) into the settings table. Runs once
/// at startup; if the equivalent setting is already in the DB, the JSON value
/// is ignored. The JSON file is left in place as a safety net.</summary>
internal static class LegacyConfigImporter
{
    private sealed class LegacyShape
    {
        public string? OutputDeviceName { get; set; }
        public string? MusicDir { get; set; }
    }

    private static string LegacyPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config", "sholto", "config.json");

    public static async Task ImportIfNeededAsync(SholtoDatabase db)
    {
        var path = LegacyPath;
        if (!File.Exists(path)) return;

        LegacyShape? legacy;
        try
        {
            legacy = JsonSerializer.Deserialize<LegacyShape>(await File.ReadAllTextAsync(path));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LegacyConfig] could not read {path}: {ex.Message}");
            return;
        }
        if (legacy is null) return;

        await ImportFieldAsync(db, SettingsKeys.MusicDir, legacy.MusicDir);
        await ImportFieldAsync(db, SettingsKeys.OutputDevice, legacy.OutputDeviceName);
    }

    private static async Task ImportFieldAsync(SholtoDatabase db, string key, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        if (await db.GetSettingAsync(key) is not null) return;  // DB wins
        await db.SetSettingAsync(key, value);
        Console.WriteLine($"[LegacyConfig] imported {key}");
    }
}
