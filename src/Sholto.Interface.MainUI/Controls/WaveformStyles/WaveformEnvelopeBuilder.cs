using Sholto.Data;

namespace Sholto.Interface.MainUI.Controls.WaveformStyles;

/// <summary>Default <see cref="IWaveformEnvelopeBuilder"/>. The arithmetic is moved unchanged from
/// <c>WaveformControl.BakeWaveform</c> (pre-C63), so the 3-BAND bake stays pixel-identical.</summary>
public sealed class WaveformEnvelopeBuilder(IWaveformBandScaler bandScaler) : IWaveformEnvelopeBuilder
{
    private const int BakedHeight = 256;
    // Bigger binPx = coarser / less detail.
    private const int BinPx = 3;               // bar width
    private const float ReleaseCoef = 0.88f;   // slow release → the tail decays over many bins = a long, curved bell fall-off
    private const float Gate = 0.06f;          // drop bins quieter than this (kills between-kick fuzz)

    private readonly IWaveformBandScaler _bandScaler = bandScaler;

    public BandEnvelopes? Build(WaveformPeaks peaks, CancellationToken ct)
    {
        int width = peaks.Min.Length;
        if (width == 0) return null;
        int height = BakedHeight;
        float midY = height / 2f;

        bool hasBands = peaks.Low.Length == width;

        // Shared-reference calibration: all bands divide by ONE broadband reference,
        // so the silhouette height tracks absolute loudness (quiet intro reads short,
        // drop reads tall) instead of every busy section maxing out. The band split
        // still colours the shape; bass dominates the height, highs a thin crest.
        var scaling = hasBands
            ? _bandScaler.CalibrateShared(peaks.Low, peaks.Mid, peaks.High)
            : default;
        // Scale the summed height (low+mid+high) so the track's fullest column nearly
        // fills the half-height.
        float scale = hasBands ? midY * 0.95f / MathF.Max(scaling.MaxTotal, 1e-3f) : midY * 0.95f;

        // Coarse, smooth "sideways bell" envelope: peak-hold the columns into bins,
        // run an attack/release follower (sharp face on the beat, graceful decay).
        int bins = (width + BinPx - 1) / BinPx;

        if (!hasBands)
        {
            var amp = new float[bins];
            for (int b = 0; b < bins; b++)
            {
                if (ct.IsCancellationRequested) return null;
                int x0 = b * BinPx, x1 = Math.Min(width, x0 + BinPx);
                float a = 0f;
                for (int x = x0; x < x1; x++)
                    a = MathF.Max(a, MathF.Max(MathF.Abs(peaks.Max[x]), MathF.Abs(peaks.Min[x])));
                amp[b] = a * midY;
            }
            AttackRelease(amp);
            return new BandEnvelopes(width, height, midY, BinPx, amp, [], [], []);
        }

        var lowH = new float[bins];
        var midH = new float[bins];
        var highH = new float[bins];
        for (int b = 0; b < bins; b++)
        {
            if (ct.IsCancellationRequested) return null;
            int x0 = b * BinPx, x1 = Math.Min(width, x0 + BinPx);
            float ml = 0f, mm = 0f, mh = 0f;
            for (int x = x0; x < x1; x++)
            {
                var (nl, nm, nh) = scaling.NormalizeAbsolute(peaks.Low[x], peaks.Mid[x], peaks.High[x]);
                if (nl > ml) ml = nl;
                if (nm > mm) mm = nm;
                if (nh > mh) mh = nh;
            }
            // Gate the quiet stuff so only real transients survive (less clutter,
            // sharper kicks) — then scale. Clamp the white core to the deck since
            // absolute normalization lets a loud transient exceed unity.
            lowH[b]  = ml < Gate ? 0f : ml * scale;
            midH[b]  = mm < Gate ? 0f : mm * scale;
            highH[b] = mh < Gate ? 0f : MathF.Min(midY, mh * scale);
        }

        AttackRelease(lowH);
        AttackRelease(midH);
        AttackRelease(highH);
        return new BandEnvelopes(width, height, midY, BinPx, null, lowH, midH, highH);
    }

    /// <summary>Attack/release envelope follower over a bin-height array: the value
    /// jumps up instantly when the signal rises (a sharp leading face at each kick)
    /// then decays geometrically per bin (the bell's tail). This is what gives the
    /// Rekordbox "sideways bell where the flat face is the beat" — a symmetric blur
    /// would round the attack away instead.</summary>
    private void AttackRelease(float[] h)
    {
        float env = 0f;
        for (int i = 0; i < h.Length; i++)
        {
            float s = h[i];
            env = s > env ? s : env * ReleaseCoef;   // instant attack, slow release
            h[i] = env;
        }
    }
}
