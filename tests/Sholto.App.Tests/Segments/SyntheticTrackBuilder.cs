using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.Data;

namespace Sholto.App.Tests.Segments;

/// <summary>Builds a believable <see cref="BasicAnalysis"/> from a list of (kind, bars).
/// 4/4, grid anchored at 0, waveform columns at <see cref="WaveformDefaults.SamplesPerPeak"/>
/// and 48 kHz. Per kind (Low = kick + bass, Mid = pads, High = hats/risers): intro is quiet
/// kick only; build has a kick and rising highs; drop is kick + bass + highs at maximum;
/// breakdown has no kick or bass, just pads; outro tails off.</summary>
internal sealed class SyntheticTrackBuilder(double bpm)
{
    private const int SampleRate = 48000;

    private readonly double _bpm = bpm;
    private readonly List<(SectionKind Kind, int Bars)> _sections = [];
    private int _leadInBars;
    private bool _loudBuild;

    public SyntheticTrackBuilder Add(SectionKind kind, int bars)
    {
        _sections.Add((kind, bars));
        return this;
    }

    /// <summary>Silent bars before the first section, so the first phrase starts at bar
    /// <paramref name="bars"/> (real grids often start 1 or 4 bars early).</summary>
    public SyntheticTrackBuilder WithLeadIn(int bars)
    {
        _leadInBars = bars;
        return this;
    }

    /// <summary>Builds are nearly as loud as drops, so the drop is only a little louder.</summary>
    public SyntheticTrackBuilder WithLoudBuild()
    {
        _loudBuild = true;
        return this;
    }

    /// <summary>intro 16, build 16, drop 32, breakdown 16, drop 32, outro 16.</summary>
    public SyntheticTrackBuilder AddStandardStructure() => Add(SectionKind.Intro, 16)
        .Add(SectionKind.Build, 16).Add(SectionKind.Drop, 32)
        .Add(SectionKind.Breakdown, 16).Add(SectionKind.Drop, 32).Add(SectionKind.Outro, 16);

    public SyntheticTrack Build()
    {
        int totalBars = _leadInBars + _sections.Sum(s => s.Bars);
        double beatPeriod = 60.0 / _bpm;
        double barSec = beatPeriod * 4;
        var grid = new Beatgrid(0.0, beatPeriod, 4, totalBars * barSec);
        var (allBeats, allDownbeats) = grid.Materialise();
        // The grid's tail guard may emit a bar or two past the end; keep exactly the track's bars.
        var beats = allBeats.Take(totalBars * 4).ToArray();
        var downbeats = allDownbeats.Take(totalBars).ToArray();

        var sections = new List<SongSection>();
        int bar = _leadInBars;
        foreach (var (kind, bars) in _sections)
        {
            sections.Add(new SongSection(kind, bar, bars));
            bar += bars;
        }

        const int spp = WaveformDefaults.SamplesPerPeak;
        int columns = (int)Math.Ceiling(totalBars * barSec * SampleRate / spp);
        var min = new float[columns];
        var max = new float[columns];
        var low = new float[columns];
        var mid = new float[columns];
        var high = new float[columns];
        for (int c = 0; c < columns; c++)
        {
            double t = (c + 0.5) * spp / SampleRate;
            var levels = LevelsAt(t, barSec, beatPeriod, sections);
            low[c] = levels.Low;
            mid[c] = levels.Mid;
            high[c] = levels.High;
            max[c] = Math.Max(levels.Low, Math.Max(levels.Mid, levels.High));
            min[c] = -max[c];
        }

        var peaks = new WaveformPeaks(min, max, low, mid, high, spp, 48000);
        var analysis = new BasicAnalysis(peaks, _bpm, beats, downbeats);
        return new SyntheticTrack(analysis, grid, sections, new PhraseGrid(_leadInBars % 8), totalBars);
    }

    private SyntheticBandLevels LevelsAt(double t, double barSec, double beatPeriod, List<SongSection> sections)
    {
        int bar = (int)(t / barSec);
        foreach (var s in sections)
        {
            if (bar < s.StartBar || bar >= s.EndBar) continue;
            double p = (t - s.StartBar * barSec) / (s.Bars * barSec);
            var (lo, mi, hi) = Envelope(s.Kind, p);
            // Kick pulse on the low band only where a kick exists.
            double beatPhase = (t % beatPeriod) / beatPeriod;
            double pulse = lo > 0.1 ? 0.85 + 0.15 * (1 - beatPhase) : 1.0;
            return new SyntheticBandLevels((float)(lo * pulse), (float)mi, (float)hi);
        }
        return new SyntheticBandLevels(0f, 0f, 0f); // lead-in: silence
    }

    private (double Low, double Mid, double High) Envelope(SectionKind kind, double p) => kind switch
    {
        SectionKind.Intro => (0.45, 0.05, 0.10),
        SectionKind.Build => _loudBuild
            ? (0.85, 0.80, 0.60 + 0.35 * p)
            : (0.50, 0.30 + 0.30 * p, 0.20 + 0.70 * p),
        SectionKind.Drop => (1.0, 0.90, 1.0),
        SectionKind.Breakdown => (0.02, 0.50, 0.15),
        SectionKind.Outro => (0.45 * (1 - p), 0.05, 0.10 * (1 - p)),
        SectionKind.Chorus => (0.80, 0.70, 0.60),
        _ => (0.60, 0.50, 0.40), // Verse, Bridge, Section
    };
}
