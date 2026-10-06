using System.Collections.Generic;
using Avalonia.Media;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>Creates a flat map keyed by dotted path from <c>defaults.json</c>.</summary>
public interface IThemeDefaultColoursFactory
{
    IReadOnlyDictionary<string, Color> Create(string json);
}
