using Avalonia.Controls;
using Avalonia.Media;

namespace Sholto.Interface.Faceplate.Views;

/// <summary>The pieces the headline and the gesture rows share, so neither copies them.</summary>
public interface IFaceplatePanelPartsFactory
{
    /// <summary>A prose <see cref="TextBlock"/> from marker text, its chips wired to the board.</summary>
    TextBlock CreateProseText(string markerText, double fontSize, FontWeight weight,
                              IBrush foreground, double lineHeight, FaceplatePanelContext context);

    /// <summary>The thin accent bar marking the row a live gesture just fired, built invisible.</summary>
    Border CreateIndicator(FaceplatePanelContext context);

    /// <summary>Lays an indicator and a row's content side by side.</summary>
    Grid CreateIndicatedRow(Border indicator, Control content);

    /// <summary>The small all-caps eyebrow over a section.</summary>
    TextBlock CreateSectionLabel(string text);
}
