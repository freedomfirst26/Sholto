namespace Sholto.Analysis;

/// <summary>
/// Pre-computed waveform peaks for rendering. Pure visual data — one column per peak.
/// Min/Max give the outline; Low/Mid/High give per-band energy [0..1] for color rendering.
/// </summary>
public sealed record WaveformPeaks(
    float[] Min,
    float[] Max,
    float[] Low,
    float[] Mid,
    float[] High,
    int SamplesPerPeak)
{
    public static WaveformPeaks Empty { get; } = new([], [], [], [], [], 512);

    /// <summary>Convenience: just the peaks, no onset envelope. Used by tests + callers
    /// that don't need beat analysis.</summary>
    public static WaveformPeaks Compute(
        float[] samples, int channels, int sampleRate = 44100, int samplesPerPeak = 1024)
        => ComputeWithOnsets(samples, channels, sampleRate, samplesPerPeak).Peaks;

    /// <summary>
    /// Computes min/max + per-band peak amplitudes from interleaved float samples,
    /// and the kick-band onset envelope (caller can use it for tempo estimation).
    /// </summary>
    internal static (WaveformPeaks Peaks, float[] KickEnvelope, int OnsetHopSamples) ComputeWithOnsets(
        float[] samples,
        int channels,
        int sampleRate,
        int samplesPerPeak = 1024)
    {
        if (samples.Length == 0) return (Empty, [], sampleRate / 100);

        int frameCount = samples.Length / channels;
        int peakCount = (frameCount + samplesPerPeak - 1) / samplesPerPeak;

        // Onset envelope frame size: ~10ms.
        int onsetHop = sampleRate / 100;
        int onsetFrameCount = frameCount / onsetHop;
        var lowEnv = new float[onsetFrameCount];

        var min = new float[peakCount];
        var max = new float[peakCount];
        var lowOut = new float[peakCount];
        var midOut = new float[peakCount];
        var highOut = new float[peakCount];

        // Biquads run on a mono mix of the source samples.
        var lpf = Biquad.LowPass(sampleRate, freq: 250f, q: 0.707f);
        var bpf = Biquad.BandPass(sampleRate, freq: 1200f, q: 0.7f);
        var hpf = Biquad.HighPass(sampleRate, freq: 4000f, q: 0.707f);
        // Separate LP for the onset detector (tighter — kick-drum band, ~150 Hz).
        var kick = Biquad.LowPass(sampleRate, freq: 150f, q: 0.707f);

        for (int p = 0; p < peakCount; p++)
        {
            float lo = 0f, hi = 0f;
            float bandLow = 0f, bandMid = 0f, bandHigh = 0f;

            int frameStart = p * samplesPerPeak;
            int frameEnd = Math.Min(frameStart + samplesPerPeak, frameCount);

            for (int f = frameStart; f < frameEnd; f++)
            {
                float mono = 0f;
                int baseIdx = f * channels;
                for (int c = 0; c < channels; c++)
                {
                    float s = samples[baseIdx + c];
                    mono += s;
                    if (s < lo) lo = s;
                    if (s > hi) hi = s;
                }
                mono /= channels;

                float l = MathF.Abs(lpf.Process(mono));
                float m = MathF.Abs(bpf.Process(mono));
                float h = MathF.Abs(hpf.Process(mono));
                float k = MathF.Abs(kick.Process(mono));

                if (l > bandLow) bandLow = l;
                if (m > bandMid) bandMid = m;
                if (h > bandHigh) bandHigh = h;

                // Track peak of the kick-band signal within the current onset hop.
                int onsetIdx = f / onsetHop;
                if (onsetIdx < onsetFrameCount && k > lowEnv[onsetIdx])
                    lowEnv[onsetIdx] = k;
            }

            min[p] = lo;
            max[p] = hi;
            lowOut[p] = bandLow;
            midOut[p] = bandMid;
            highOut[p] = bandHigh;
        }

        Smooth(lowOut, radius: 2);
        Smooth(midOut, radius: 2);
        Smooth(highOut, radius: 2);
        Smooth(min, radius: 2);
        Smooth(max, radius: 2);

        Normalize(lowOut);
        Normalize(midOut);
        Normalize(highOut);

        var peaks = new WaveformPeaks(min, max, lowOut, midOut, highOut, samplesPerPeak);
        return (peaks, lowEnv, onsetHop);
    }

    /// <summary>(2*radius + 1)-tap box smoothing in place.</summary>
    private static void Smooth(float[] arr, int radius)
    {
        int n = arr.Length;
        if (n == 0 || radius <= 0) return;
        var tmp = new float[n];
        int window = 2 * radius + 1;
        for (int i = 0; i < n; i++)
        {
            float sum = 0;
            int count = 0;
            int from = Math.Max(0, i - radius);
            int to = Math.Min(n - 1, i + radius);
            for (int j = from; j <= to; j++) { sum += arr[j]; count++; }
            tmp[i] = sum / count;
        }
        Array.Copy(tmp, arr, n);
    }

    /// <summary>
    /// Ellis 2007 beat tracker:
    ///   1) Onset envelope from the kick-band signal (we get this for free upstream).
    ///   2) Autocorrelation → global tempo period.
    ///   3) Dynamic programming → the sequence of beat times that maximise
    ///      onset strength while staying close to the global period.
    /// Beats snap to real onsets, so the grid survives tempo wobble and intros
    /// without a steady kick.
    /// </summary>
    internal static (double Bpm, double[] BeatTimes) EstimateTempo(
        float[] env, int hopSamples, int sampleRate)
    {
        int n = env.Length;
        if (n < 200) return (0.0, []);

        // ── 1. Onset envelope: half-wave-rectified derivative, then normalised. ──
        var onset = new float[n];
        for (int i = 1; i < n; i++)
        {
            float d = env[i] - env[i - 1];
            onset[i] = d > 0 ? d : 0;
        }
        // Local-mean subtraction to remove slow drift (helps in busy verses).
        SubtractLocalMean(onset, radius: 8);
        Normalize(onset);

        // ── 2. Global tempo via autocorrelation in the 80–160 BPM band. ──
        double framesPerSec = (double)sampleRate / hopSamples;
        int minLag = (int)Math.Round(framesPerSec * 60.0 / 180.0);
        int maxLag = (int)Math.Round(framesPerSec * 60.0 / 60.0);
        if (maxLag >= n / 2) maxLag = n / 2 - 1;

        double bestScore = -1;
        int bestLag = minLag;
        for (int lag = minLag; lag <= maxLag; lag++)
        {
            double sum = 0;
            int limit = n - lag;
            for (int i = 0; i < limit; i++) sum += onset[i] * onset[i + lag];
            if (sum > bestScore) { bestScore = sum; bestLag = lag; }
        }

        double bpm = 60.0 * framesPerSec / bestLag;
        while (bpm < 80)  { bpm *= 2; bestLag /= 2; }
        while (bpm > 160) { bpm /= 2; bestLag *= 2; }

        // ── 3. Dynamic-programming beat sequence. ──
        // For each frame t, find the previous beat τ in [t − 2P, t − P/2] that
        // maximises  cumscore[τ]  −  tightness · ( ln(t − τ) − ln P )².
        // The log-distance penalty (Ellis) pushes consecutive beats toward the
        // global period without forcing them exactly there.
        const double tightness = 100.0;
        double lnP = Math.Log(bestLag);
        int searchMin = Math.Max(1, bestLag / 2);
        int searchMax = bestLag * 2;

        var cumscore = new float[n];
        var backlink = new int[n];
        Array.Fill(backlink, -1);

        for (int t = 0; t < n; t++)
        {
            int lo = Math.Max(0, t - searchMax);
            int hi = t - searchMin;
            double bestPrevScore = float.NegativeInfinity;
            int bestPrev = -1;
            for (int tau = lo; tau <= hi; tau++)
            {
                double dt = Math.Log(t - tau) - lnP;
                double s = cumscore[tau] - tightness * dt * dt;
                if (s > bestPrevScore) { bestPrevScore = s; bestPrev = tau; }
            }
            if (bestPrev < 0)
            {
                cumscore[t] = onset[t];
                backlink[t] = -1;
            }
            else
            {
                cumscore[t] = onset[t] + (float)bestPrevScore;
                backlink[t] = bestPrev;
            }
        }

        // Trace-back from the strongest beat in the final stretch (avoids edge effects).
        int tailStart = n - Math.Min(n, searchMax);
        int endIdx = tailStart;
        float endBest = float.NegativeInfinity;
        for (int i = tailStart; i < n; i++)
            if (cumscore[i] > endBest) { endBest = cumscore[i]; endIdx = i; }

        var beatFrames = new List<int>();
        for (int t = endIdx; t >= 0; t = backlink[t])
        {
            beatFrames.Add(t);
            if (backlink[t] < 0) break;
        }
        beatFrames.Reverse();

        var beats = new double[beatFrames.Count];
        double secsPerFrame = 1.0 / framesPerSec;
        for (int i = 0; i < beatFrames.Count; i++)
            beats[i] = beatFrames[i] * secsPerFrame;

        return (Math.Round(bpm * 10) / 10.0, beats);
    }

    private static void SubtractLocalMean(float[] arr, int radius)
    {
        int n = arr.Length;
        if (n == 0) return;
        var tmp = new float[n];
        for (int i = 0; i < n; i++)
        {
            float sum = 0;
            int count = 0;
            int lo = Math.Max(0, i - radius);
            int hi = Math.Min(n - 1, i + radius);
            for (int j = lo; j <= hi; j++) { sum += arr[j]; count++; }
            tmp[i] = arr[i] - sum / count;
            if (tmp[i] < 0) tmp[i] = 0;
        }
        Array.Copy(tmp, arr, n);
    }

    private static void Normalize(float[] arr)
    {
        float max = 0f;
        for (int i = 0; i < arr.Length; i++)
            if (arr[i] > max) max = arr[i];
        if (max <= 0f) return;
        float inv = 1f / max;
        for (int i = 0; i < arr.Length; i++) arr[i] *= inv;
    }
}

/// <summary>RBJ Audio EQ Cookbook biquad (direct form I), per-channel state.</summary>
internal struct Biquad
{
    public float B0, B1, B2, A1, A2;
    public float X1, X2, Y1, Y2;

    public float Process(float x)
    {
        float y = B0 * x + B1 * X1 + B2 * X2 - A1 * Y1 - A2 * Y2;
        X2 = X1; X1 = x;
        Y2 = Y1; Y1 = y;
        return y;
    }

    public static Biquad LowPass(int sampleRate, float freq, float q)
    {
        double w0 = 2 * Math.PI * freq / sampleRate;
        double cosW = Math.Cos(w0);
        double alpha = Math.Sin(w0) / (2 * q);
        double b0 = (1 - cosW) / 2;
        double b1 = 1 - cosW;
        double b2 = (1 - cosW) / 2;
        double a0 = 1 + alpha;
        double a1 = -2 * cosW;
        double a2 = 1 - alpha;
        return new Biquad
        {
            B0 = (float)(b0 / a0), B1 = (float)(b1 / a0), B2 = (float)(b2 / a0),
            A1 = (float)(a1 / a0), A2 = (float)(a2 / a0)
        };
    }

    public static Biquad BandPass(int sampleRate, float freq, float q)
    {
        double w0 = 2 * Math.PI * freq / sampleRate;
        double cosW = Math.Cos(w0);
        double alpha = Math.Sin(w0) / (2 * q);
        double b0 = alpha;
        double b1 = 0;
        double b2 = -alpha;
        double a0 = 1 + alpha;
        double a1 = -2 * cosW;
        double a2 = 1 - alpha;
        return new Biquad
        {
            B0 = (float)(b0 / a0), B1 = (float)(b1 / a0), B2 = (float)(b2 / a0),
            A1 = (float)(a1 / a0), A2 = (float)(a2 / a0)
        };
    }

    public static Biquad HighPass(int sampleRate, float freq, float q)
    {
        double w0 = 2 * Math.PI * freq / sampleRate;
        double cosW = Math.Cos(w0);
        double alpha = Math.Sin(w0) / (2 * q);
        double b0 = (1 + cosW) / 2;
        double b1 = -(1 + cosW);
        double b2 = (1 + cosW) / 2;
        double a0 = 1 + alpha;
        double a1 = -2 * cosW;
        double a2 = 1 - alpha;
        return new Biquad
        {
            B0 = (float)(b0 / a0), B1 = (float)(b1 / a0), B2 = (float)(b2 / a0),
            A1 = (float)(a1 / a0), A2 = (float)(a2 / a0)
        };
    }
}
