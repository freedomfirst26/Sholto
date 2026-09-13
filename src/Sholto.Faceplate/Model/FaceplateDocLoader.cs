using System.Text.Json;

namespace Sholto.Faceplate.Model;

/// <summary>Reads a device's guide file. The shipped copy is embedded in the
/// assembly so the single-file release stays self-contained; an override in the
/// user's data directory wins if it exists, so wording can be fixed without a
/// rebuild.</summary>
public sealed class FaceplateDocLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Path an override would live at, whether or not it exists.</summary>
    public string OverridePath(string deviceKey) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "sholto", $"{deviceKey}.guide.json");

    public FaceplateDoc LoadEmbedded(string deviceKey)
    {
        var overridePath = OverridePath(deviceKey);
        if (File.Exists(overridePath))
            return Parse(File.ReadAllText(overridePath), overridePath);

        var name = typeof(FaceplateDocLoader).Assembly
            .GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith($"{deviceKey}.guide.json", StringComparison.Ordinal))
            ?? throw new FileNotFoundException($"No embedded guide for device '{deviceKey}'.");

        using var stream = typeof(FaceplateDocLoader).Assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd(), name);
    }

    private static FaceplateDoc Parse(string json, string source)
    {
        try
        {
            return JsonSerializer.Deserialize<FaceplateDoc>(json, Options)
                   ?? throw new InvalidDataException($"{source} parsed to null.");
        }
        catch (JsonException ex)
        {
            // A malformed override must say so plainly rather than opening a blank overlay.
            throw new InvalidDataException($"{source} is not valid guide JSON: {ex.Message}", ex);
        }
    }
}
