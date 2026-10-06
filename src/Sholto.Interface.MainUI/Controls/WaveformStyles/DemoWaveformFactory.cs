using Sholto.Interface.MainUI.ViewModels;
using Sholto.Data;

namespace Sholto.Interface.MainUI.Controls.WaveformStyles;

/// <summary>Default <see cref="IDemoWaveformFactory"/>. The signal model is ported from the approved
/// Layout Wizard mockup: 128 BPM, 256 beats, each beat split into eight slots (kick on the beat, bass
/// and snare/clap on the off-beats, hats on the even slots), with a seeded LCG for the noise floor.
/// Raw band levels only: the style strategies apply their own attack/release.</summary>
public sealed class DemoWaveformFactory : IDemoWaveformFactory
{
    private const uint Seed = 20261004;
    private const int Beats = 256;
    private const int SlotsPerBeat = 8;
    private const double DemoBpm = 128;
    private const int DemoSampleRate = 48000;

    private readonly Lazy<WaveformPeaks> _peaks;

    public DemoWaveformFactory() => _peaks = new Lazy<WaveformPeaks>(Build);

    public WaveformPeaks Peaks => _peaks.Value;

    public double Bpm => DemoBpm;

    private WaveformPeaks Build()
    {
        // The analyser's WaveformDefaults.SamplesPerPeak; DemoWaveformFactoryTests pins the match.
        const int samplesPerPeak = 1024;
        double peaksPerBeat = 60.0 / DemoBpm * DemoSampleRate / samplesPerPeak;
        int n = (int)Math.Round(Beats * peaksPerBeat);
        var low = new float[n];
        var mid = new float[n];
        var high = new float[n];
        uint seed = Seed;
        float Rnd()
        {
            seed = unchecked(seed * 1664525u + 1013904223u);
            return (float)(seed / 4294967296.0);
        }

        for (int i = 0; i < n; i++)
        {
            double beatPos = i / peaksPerBeat;
            int b = Math.Min(Beats - 1, (int)beatPos);
            double p = beatPos - b;
            int s = Math.Min(SlotsPerBeat - 1, (int)(p * SlotsPerBeat));
            int bi = b % 4;
            var sec = SectionAt(b);
            float l = .03f, m = .04f, h = .03f;
            bool kick = sec is Section.Intro or Section.Groove or Section.Drop or Section.Outro
                || (sec == Section.Build && b < 88);
            float kAmp = sec switch
            {
                Section.Drop => 1.15f,
                Section.Outro => .75f * (1f - (b - 232) / 30f),
                Section.Intro => .8f,
                _ => 1f,
            };
            if (kick)
            {
                // A kick's body lasts about an eighth (≈ 5 columns), not one column.
                l += kAmp * (float)Math.Exp(-p * 4);
                m += .15f * kAmp * (float)Math.Exp(-p * 9);
            }
            if (sec is Section.Groove or Section.Drop || (sec == Section.Build && b < 80))
            {
                // Bassline note after the off-beat hat, not under it, so the hat's column is its own.
                float bass = sec == Section.Drop ? .7f : .5f;
                l += (s >= 5 && s < 7) ? bass : .06f;
            }
            if (kick || sec == Section.Build)
            {
                // Open hat on the off-beat with a synth stab under it (mids + highs → the cyan stroke).
                if (s == 4) { h += sec == Section.Drop ? .75f : .55f; m += sec == Section.Drop ? .4f : .25f; }
                if (s % 2 == 0) h += .12f;
            }
            if (sec is Section.Groove or Section.Drop && (bi == 1 || bi == 3) && s < 2)
            {
                m += s != 0 ? .35f : .6f;
                h += .35f;
            }
            if (sec == Section.Groove && b % 8 == 6 && s == 6) m += .45f;
            if (sec == Section.Drop && (s == 3 || s == 6) && b % 2 == 1) m += .42f;
            if (sec is Section.Build or Section.Roll)
            {
                float t = sec == Section.Roll ? 1f : (b - 64) / 32f;
                int step = t < .35f ? 4 : t < .7f ? 2 : 1;
                if (s % step == 0) m += .25f + .35f * t;
                h += .1f + .45f * (sec == Section.Roll ? 1f : t) * (.6f + .4f * Rnd());
                if (sec == Section.Roll && b >= 135) { m *= .12f; h *= .15f; }
            }
            if (sec == Section.Break)
            {
                float t = (b - 96) / 32f;
                m += .42f + .12f * (float)Math.Sin(i / 22.0 * 8 / peaksPerBeat) + ((b % 8 < 4 && s < 5) ? .18f : 0f);
                l += .12f + .05f * (float)Math.Sin(i / 40.0 * 8 / peaksPerBeat);
                h += .08f + .25f * t * t;
            }
            l += Rnd() * .05f;
            m += Rnd() * .06f;
            h += Rnd() * .06f;
            low[i] = l; mid[i] = m; high[i] = h;
        }

        float peak = 0f;
        for (int i = 0; i < n; i++) peak = Math.Max(peak, Math.Max(low[i], Math.Max(mid[i], high[i])));
        float norm = 1f / peak;
        var min = new float[n];
        var max = new float[n];
        for (int i = 0; i < n; i++)
        {
            low[i] *= norm; mid[i] *= norm; high[i] *= norm;
            // Sample amplitude the way a mastered mix has it: the bass carries it, mids and hats less so.
            float total = Math.Min(1f, low[i] + .7f * mid[i] + .5f * high[i]);
            max[i] = total;
            min[i] = -total;
        }
        // The analyzer box-smooths every array over ±2 columns (WaveformPeakAnalyzer); match it so the
        // demo has the same column-to-column statistics as a real analysed track.
        Smooth(low); Smooth(mid); Smooth(high); Smooth(min); Smooth(max);
        return new WaveformPeaks(min, max, low, mid, high, samplesPerPeak, DemoSampleRate);
    }

    private void Smooth(float[] a)
    {
        const int radius = 2;
        var src = (float[])a.Clone();
        for (int i = 0; i < a.Length; i++)
        {
            int from = Math.Max(0, i - radius), to = Math.Min(a.Length - 1, i + radius);
            float sum = 0f;
            for (int j = from; j <= to; j++) sum += src[j];
            a[i] = sum / (to - from + 1);
        }
    }

    private Section SectionAt(int b) =>
        b < 16 ? Section.Intro
        : b < 64 ? Section.Groove
        : b < 96 ? Section.Build
        : b < 128 ? Section.Break
        : b < 136 ? Section.Roll
        : b < 200 ? Section.Drop
        : b < 232 ? Section.Groove
        : Section.Outro;

    private enum Section { Intro, Groove, Build, Break, Roll, Drop, Outro }
}
