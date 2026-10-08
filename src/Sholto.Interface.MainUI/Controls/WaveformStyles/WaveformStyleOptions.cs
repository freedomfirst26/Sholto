namespace Sholto.Interface.MainUI.Controls.WaveformStyles;

/// <summary>Waveform style look, supplied via the standard <c>IOptions&lt;WaveformStyleOptions&gt;</c> pipeline like
/// <see cref="Sholto.Interface.MainUI.FeatureOptions"/>. Defaults live here.</summary>
public sealed class WaveformStyleOptions
{
    /// <summary>RGB style: share of the weakest band subtracted before mixing colours (1 = winner takes all,
    /// 0 = plain weights).</summary>
    public float RgbFloorShare { get; set; } = 0.75f;

    /// <summary>RGB style: columns either side averaged into each column's colour (about 0.35 s at 21 ms a column).</summary>
    public int RgbColourRadius { get; set; } = 8;
}
