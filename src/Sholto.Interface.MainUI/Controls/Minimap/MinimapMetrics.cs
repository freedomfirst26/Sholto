namespace Sholto.Interface.MainUI.Controls.Minimap;

/// <summary>The minimap strip's fixed heights, in DIPs. Only constants: the strip is a label row above a
/// whole-track waveform body, and a deck row is the strip plus the 218-DIP scrolling waveform.</summary>
public sealed class MinimapMetrics
{
    /// <summary>The whole strip: <see cref="LabelRowHeight"/> + a 50-DIP waveform body.</summary>
    public const double StripHeight = 64;

    /// <summary>Section names and their coloured underlines.</summary>
    public const double LabelRowHeight = 14;

    /// <summary>The coloured underline under each section name, the bottom of the label row.</summary>
    public const double UnderlineHeight = 3;

    /// <summary>The least spacing, in DIPs, between neighbouring phrase gaps in the underline strip; closer
    /// gaps of the weaker weights are dropped.</summary>
    public const double MinPhraseSpacing = 6;
}
