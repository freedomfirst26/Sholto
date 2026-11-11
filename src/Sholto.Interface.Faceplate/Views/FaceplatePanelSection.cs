using Avalonia.Controls;

namespace Sholto.Interface.Faceplate.Views;

/// <summary>A built run of panel children and the active-row indicators inside them, by gesture id.</summary>
public sealed record FaceplatePanelSection(
    IReadOnlyList<Control> Children, IReadOnlyDictionary<string, Border> Indicators);
