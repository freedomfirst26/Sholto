using System.Collections.Generic;
using System.Text.Json;
using Avalonia.Media;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>Flattens nested objects of colour strings into dotted keys
/// (<c>{ "waveform": { "background": "#RRGGBB" } }</c> becomes <c>waveform.background</c>).</summary>
public sealed class ThemeDefaultColoursFactory : IThemeDefaultColoursFactory
{
    public IReadOnlyDictionary<string, Color> Create(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var result = new Dictionary<string, Color>();
        Flatten(doc.RootElement, "", result);
        return result;
    }

    private void Flatten(JsonElement e, string prefix, Dictionary<string, Color> into)
    {
        foreach (var prop in e.EnumerateObject())
        {
            var key = prefix.Length == 0 ? prop.Name : prefix + "." + prop.Name;
            if (prop.Value.ValueKind == JsonValueKind.Object) Flatten(prop.Value, key, into);
            else into[key] = Color.Parse(prop.Value.GetString()!);
        }
    }
}
