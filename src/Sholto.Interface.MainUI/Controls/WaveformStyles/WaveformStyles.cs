namespace Sholto.Interface.MainUI.Controls.WaveformStyles;

/// <summary>Default <see cref="IWaveformStyles"/>. The first style given is the default.</summary>
public sealed class WaveformStyles : IWaveformStyles
{
    public WaveformStyles(IReadOnlyList<IWaveformStyleStrategy> all)
    {
        if (all.Count == 0) throw new ArgumentException("At least one waveform style is required.", nameof(all));
        All = all;
    }

    public IReadOnlyList<IWaveformStyleStrategy> All { get; }

    public IWaveformStyleStrategy Default => All[0];

    public IWaveformStyleStrategy ById(string? id) =>
        All.FirstOrDefault(s => s.Id == id) ?? Default;
}
