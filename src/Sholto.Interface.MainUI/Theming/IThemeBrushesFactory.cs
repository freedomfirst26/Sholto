using Avalonia.Controls;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>Builds the "Sholto…" resources a theme publishes (brushes, palettes, shadows, tints) for the window's
/// {DynamicResource} references.</summary>
public interface IThemeBrushesFactory
{
    /// <summary>Every themed key for <paramref name="theme"/>, in a new dictionary. A host swaps it in as one merged
    /// dictionary, replacing the previous theme's, so the tree sees a single change instead of one per key.</summary>
    ResourceDictionary Create(SholtoTheme theme);
}
