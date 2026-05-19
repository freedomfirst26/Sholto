using System.Text.Json;

namespace Sholto.App;

public sealed class AppConfig
{
    public string? OutputDeviceName { get; set; }
    public string? MusicDir { get; set; }

    private static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config", "sholto", "config.json");

    // Two startup tasks (library scan + audio init) both update the config in
    // parallel. We lock around every read-modify-write so neither clobbers the
    // other's field.
    private static readonly object _gate = new();

    public static AppConfig Load()
    {
        lock (_gate)
        {
            try
            {
                if (File.Exists(ConfigPath))
                    return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath)) ?? new AppConfig();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load config: {ex.Message}");
            }
            return new AppConfig();
        }
    }

    /// <summary>Apply a single-field update without losing any other fields a
    /// concurrent writer may have set since we last loaded.</summary>
    public static void Update(Action<AppConfig> change)
    {
        lock (_gate)
        {
            var cfg = Load();
            change(cfg);
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath,
                JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
