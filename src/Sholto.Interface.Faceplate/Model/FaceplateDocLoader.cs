using System.Text.Json;

namespace Sholto.Interface.Faceplate.Model;

/// <summary>Reads a device's guide file. The shipped copy is embedded in the
/// assembly so the single-file release stays self-contained; an override in the
/// user's data directory wins if it exists, so wording can be fixed without a
/// rebuild.</summary>
public sealed class FaceplateDocLoader
{
    private readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Path an override would live at, whether or not it exists.</summary>
    public string OverridePath(string deviceKey) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "sholto", $"{deviceKey}.guide.json");

    public FaceplateDoc Load(IDeviceFaceplate device)
    {
        var overridePath = OverridePath(device.DeviceKey);
        if (File.Exists(overridePath))
            return Parse(File.ReadAllText(overridePath), overridePath);

        using var stream = device.OpenGuide();
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd(), $"{device.DeviceKey} embedded guide");
    }

    private FaceplateDoc Parse(string json, string source)
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
