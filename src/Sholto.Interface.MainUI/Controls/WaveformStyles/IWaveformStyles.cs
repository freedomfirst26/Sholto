namespace Sholto.Interface.MainUI.Controls.WaveformStyles;

/// <summary>The waveform styles the DJ can choose between, in the order the wizard offers them.</summary>
public interface IWaveformStyles
{
    IReadOnlyList<IWaveformStyleStrategy> All { get; }

    /// <summary>The style used when nothing was chosen: 3-BAND.</summary>
    IWaveformStyleStrategy Default { get; }

    /// <summary>The style with that <see cref="IWaveformStyleStrategy.Id"/>, or <see cref="Default"/>
    /// when the id is null or unknown (e.g. a saved id from a build that had a style this one lacks).</summary>
    IWaveformStyleStrategy ById(string? id);
}
