using System;
using System.Collections.Generic;
using System.Text.Json;
using Avalonia.Media;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>Reads <c>{ "&lt;PresetName&gt;": { "low", "mid", "high", "downbeat" } }</c>;
/// colours are <c>#RRGGBB</c> or <c>#AARRGGBB</c>.</summary>
public sealed class WaveformPresetCatalogueFactory : IWaveformPresetCatalogueFactory
{
    public IReadOnlyDictionary<WaveformPreset, WaveformPresetColours> Create(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var result = new Dictionary<WaveformPreset, WaveformPresetColours>();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var preset = Enum.Parse<WaveformPreset>(prop.Name);
            var e = prop.Value;
            result[preset] = new WaveformPresetColours(
                Read(e, "low"), Read(e, "mid"), Read(e, "high"), Read(e, "downbeat"));
        }
        return result;
    }

    private Color Read(JsonElement e, string key) => Color.Parse(e.GetProperty(key).GetString()!);
}
