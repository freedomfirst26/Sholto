using Sholto.Data;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.ViewModels;

public sealed class DeckAnalysisDisplay(WaveformPeaks noPeaks, IThemeContext theme) : IDeckAnalysisDisplay
{
    // The one "no peaks" value, so Peaks/GridPeaks do not allocate per read.
    private readonly WaveformPeaks _noPeaks = noPeaks;
    private readonly IThemeContext _theme = theme;
    private DeckAnalysis? _analysis;

    public void Set(DeckAnalysis? analysis) => _analysis = analysis;

    public DeckAnalysis? Analysis => _analysis;
    public WaveformPeaks Peaks => _analysis?.Peaks ?? _noPeaks;
    public WaveformPeaks GridPeaks => _analysis?.Peaks ?? _noPeaks;
    public IReadOnlyList<VocalRegion>? VocalRegions => _analysis?.VocalRegions;
    public double[] BeatTimes => _analysis?.BeatTimes ?? [];
    public double[] DownbeatTimes => _analysis?.DownbeatTimes ?? [];
    public bool HasAnalysis => _analysis?.HasBasic == true;
    public bool HasStems => _analysis?.HasStems == true;
    public bool HasKey => LoadedKey is not null;
    public string Camelot => LoadedKey?.ToCamelot() ?? "";

    private KeyRef? LoadedKey => _analysis?.Key;

    public Avalonia.Media.IBrush KeyBrush
    {
        get
        {
            if (LoadedKey is not { } key) return Avalonia.Media.Brushes.Transparent;
            var p = _theme.Current.CamelotPalette;
            return p.KeyBrush(key);
        }
    }

    public float LowAt(double playPosition) =>
        _analysis?.Peaks is { } pk ? BandAt(pk, pk.Low, playPosition) : 0f;

    public float MidAt(double playPosition) =>
        _analysis?.Peaks is { } pk ? BandAt(pk, pk.Mid, playPosition) : 0f;

    public float HighAt(double playPosition) =>
        _analysis?.Peaks is { } pk ? BandAt(pk, pk.High, playPosition) : 0f;

    private float BandAt(WaveformPeaks pk, float[] band, double playPosition)
    {
        if (pk.Min.Length == 0) return 0f;
        if (band.Length != pk.Min.Length) return 0f;
        int col = Math.Clamp((int)(playPosition * pk.Min.Length), 0, pk.Min.Length - 1);
        return band[col];
    }
}
