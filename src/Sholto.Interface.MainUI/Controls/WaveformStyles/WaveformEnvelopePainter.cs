using SkiaSharp;

namespace Sholto.Interface.MainUI.Controls.WaveformStyles;

/// <summary>Default <see cref="IWaveformEnvelopePainter"/>; moved unchanged from
/// <c>WaveformControl.FillEnvelope</c> (pre-C63).</summary>
public sealed class WaveformEnvelopePainter : IWaveformEnvelopePainter
{
    /// <summary>Fill one band's envelope as a single closed path, mirrored above and
    /// below the centerline. Rises are drawn as a VERTICAL leading face (a flat-faced
    /// bell whose flat front sits on the beat), while falls ramp smoothly to the bin
    /// centre so the tail stays a graceful bell rather than a staircase.</summary>
    public void Fill(SKCanvas canvas, float[] h, int binPx, int width, float midY, SKPaint paint)
    {
        int n = h.Length;
        if (n == 0) return;
        float X(int b) => MathF.Min(width, b * binPx + binPx * 0.5f);

        // Build the top silhouette once, then mirror it exactly for the bottom so the
        // two faces are guaranteed symmetric.
        var xs = new List<float>(n + 4);
        var hs = new List<float>(n + 4);
        void P(float x, float ht) { xs.Add(x); hs.Add(ht); }

        P(0, h[0]);
        for (int b = 1; b < n; b++)
        {
            if (h[b] > h[b - 1])
            {
                // Rising into a beat: hold the old height to this bin's left edge,
                // then jump straight up — a flat vertical front on the kick.
                float xl = MathF.Min(width, b * binPx);
                P(xl, h[b - 1]);
                P(xl, h[b]);
            }
            else
            {
                // Decaying: ramp to the bin centre for the smooth bell tail.
                P(X(b), h[b]);
            }
        }
        P(width, h[n - 1]);

        using var path = new SKPath();
        path.MoveTo(xs[0], midY - hs[0]);
        for (int i = 1; i < xs.Count; i++) path.LineTo(xs[i], midY - hs[i]);
        for (int i = xs.Count - 1; i >= 0; i--) path.LineTo(xs[i], midY + hs[i]);
        path.Close();
        canvas.DrawPath(path, paint);
    }
}
