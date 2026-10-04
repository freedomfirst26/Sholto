using System.Collections.Generic;
using Avalonia.Media;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>Parses <c>defaults.json</c> into a flat map keyed by dotted path.</summary>
public interface IThemeDefaultsJson
{
    IReadOnlyDictionary<string, Color> Parse(string json);
}
