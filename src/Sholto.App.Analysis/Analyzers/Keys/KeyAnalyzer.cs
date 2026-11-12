using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Harmony;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;

namespace Sholto.App.Analysis.Analyzers.Keys;

/// <summary>
/// Pure-C# musical-key estimator. No FFT lib needed: a bank of Goertzel filters
/// computes per-frame energy at every pitch we care about, those collapse into a
/// 12-bin chroma vector, and the chroma is correlated against the 24
/// Krumhansl-Schmuckler key profiles. Best correlation = the key.
///
/// Accuracy is ~95% on dance music with a clear tonal centre; weaker on noise /
/// percussion-heavy intros. Runs in a few seconds on a 6-min track in-process,
/// no Python subprocess, no native dep.
///
/// Default <see cref="IKeyAnalyzer"/>. Instance so a test/Bench harness can
/// substitute a fake instead of paying the real chroma + correlation cost;
/// it holds no state beyond its configured <see cref="KeyAnalysisOptions"/>.
/// </summary>
public sealed class KeyAnalyzer : IKeyAnalyzer
{
    /// <summary>Semitones in an octave: the number of chroma bins, pitch classes and
    /// key-profile entries. A fact of twelve-tone equal temperament, not a tunable.</summary>
    private const int SemitonesPerOctave = 12;

    /// <summary>MIDI note number of A4, the note <see cref="KeyAnalysisOptions.ReferencePitchHz"/> tunes.</summary>
    private const int ReferenceMidiNote = 69;

    private readonly KeyAnalysisOptions _options;

    public KeyAnalyzer(IOptions<KeyAnalysisOptions> options)
    {
        _options = options.Value;
        if (_options.LastMidiNote < _options.FirstMidiNote)
            throw new ArgumentException(
                $"LastMidiNote ({_options.LastMidiNote}) is below FirstMidiNote ({_options.FirstMidiNote}).",
                nameof(options));
        if (_options.FramesPerChunk < 1)
            throw new ArgumentException(
                $"FramesPerChunk ({_options.FramesPerChunk}) must be at least 1.", nameof(options));
    }

    /// <summary>Fire-and-await wrapper: runs the heavy chroma + correlation on a
    /// background task and reports progress like the other analyzers.</summary>
    public async Task<KeyAnalysis> AnalyzeAsync(
        DecodedTrack track,
        IAnalysisReporter reporter, CancellationToken ct = default)
    {
        reporter.Running(track.FilePath, AnalysisSteps.Key);
        try
        {
            var key = await Task.Run(
                () => Estimate(track.StereoSamples, track.Channels, track.SampleRate, ct), ct);
            var analysis = new KeyAnalysis(key);
            reporter.Complete(track.FilePath, AnalysisSteps.Key, key.ToCamelot());
            return analysis;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Superseded by a newer load: not a failure, and not still running either.
            reporter.Cancelled(track.FilePath, AnalysisSteps.Key);
            throw;
        }
        catch (Exception ex)
        {
            reporter.Failed(track.FilePath, AnalysisSteps.Key, ex.Message);
            throw;
        }
    }

    // Standard Krumhansl-Schmuckler tonal profiles (Temperley revision values).
    // Index 0 = tonic, 1 = +1 semitone, … 11 = +11 semitones above the tonic.
    private readonly double[] MajorProfile =
        { 6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88 };
    private readonly double[] MinorProfile =
        { 6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17 };

    private Key Estimate(float[] stereoSamples, int channels, int sampleRate, CancellationToken ct)
    {
        var chroma = ComputeChroma(stereoSamples, channels, sampleRate, ct);

        // Normalise so dot-product with the profile is rotation-equivalent across tracks.
        double sum = 0;
        for (int i = 0; i < SemitonesPerOctave; i++) sum += chroma[i];
        if (sum > 0) for (int i = 0; i < SemitonesPerOctave; i++) chroma[i] /= sum;

        double bestScore = double.NegativeInfinity;
        int bestTonic = 0;
        bool bestMajor = true;
        for (int tonic = 0; tonic < SemitonesPerOctave; tonic++)
        {
            double majScore = Correlate(chroma, MajorProfile, tonic);
            if (majScore > bestScore) { bestScore = majScore; bestTonic = tonic; bestMajor = true; }
            double minScore = Correlate(chroma, MinorProfile, tonic);
            if (minScore > bestScore) { bestScore = minScore; bestTonic = tonic; bestMajor = false; }
        }

        return new Key(bestTonic, bestMajor);
    }

    private double Correlate(double[] chroma, double[] profile, int shift)
    {
        double s = 0;
        for (int i = 0; i < SemitonesPerOctave; i++)
            s += chroma[i] * profile[((i - shift) % SemitonesPerOctave + SemitonesPerOctave) % SemitonesPerOctave];
        return s;
    }

    /// <summary>
    /// Build a 12-bin chroma vector by running one Goertzel filter per note from
    /// <see cref="KeyAnalysisOptions.FirstMidiNote"/> to <see cref="KeyAnalysisOptions.LastMidiNote"/>
    /// (by default 60 filters: 12 pitch classes × 5 octaves, C2 → B6) frame-by-frame and summing each pitch class
    /// across its octaves. 4096-sample frames at 48 kHz ≈ 85 ms — short enough
    /// to catch fast chord changes, long enough to give the filter resolution.
    /// </summary>
    private double[] ComputeChroma(float[] samples, int channels, int sampleRate, CancellationToken ct)
    {
        const int frameSize = 4096;
        int firstMidi = _options.FirstMidiNote;
        int lastMidi = _options.LastMidiNote;
        int numNotes = lastMidi - firstMidi + 1;

        // Pre-compute Goertzel coefficient (2·cos(ω)) for every note.
        var coefs = new double[numNotes];
        for (int n = 0; n < numNotes; n++)
        {
            double freq = _options.ReferencePitchHz
                * Math.Pow(2, (firstMidi + n - ReferenceMidiNote) / (double)SemitonesPerOctave);
            coefs[n] = 2.0 * Math.Cos(2 * Math.PI * freq / sampleRate);
        }

        int frameCount = samples.Length / channels;
        int totalFrames = frameCount / frameSize;

        // Frames are independent, so they are spread over cores in fixed chunks. Each chunk
        // accumulates its own partial chroma and the partials are summed in chunk order, so
        // the result is the same however the threads happen to be scheduled. Half the cores
        // at most: madmom runs beside this and must not be starved (measured: no slowdown).
        int framesPerChunk = _options.FramesPerChunk;
        int chunkCount = (totalFrames + framesPerChunk - 1) / framesPerChunk;
        var partials = new double[chunkCount][];
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2),
            CancellationToken = ct,
        };

        Parallel.For(0, chunkCount, options, chunk =>
        {
            var partial = new double[SemitonesPerOctave];
            var mono = new float[frameSize];
            int lastFrame = Math.Min(totalFrames, (chunk + 1) * framesPerChunk);
            for (int frame = chunk * framesPerChunk; frame < lastFrame; frame++)
            {
                int frameStart = frame * frameSize;

                // Stereo → mono mix once per frame, then Goertzel reads from a contiguous buffer.
                for (int i = 0; i < frameSize; i++)
                {
                    int idx = (frameStart + i) * channels;
                    float m = 0f;
                    for (int c = 0; c < channels; c++) m += samples[idx + c];
                    mono[i] = m / channels;
                }

                for (int n = 0; n < numNotes; n++)
                {
                    double s1 = 0, s2 = 0;
                    double coef = coefs[n];
                    for (int i = 0; i < frameSize; i++)
                    {
                        double s = mono[i] + coef * s1 - s2;
                        s2 = s1; s1 = s;
                    }
                    double power = s1 * s1 + s2 * s2 - coef * s1 * s2;
                    if (power > 0)
                    {
                        int pitchClass = (firstMidi + n) % SemitonesPerOctave;
                        partial[pitchClass] += Math.Sqrt(power);
                    }
                }
            }
            partials[chunk] = partial;
        });

        var chroma = new double[SemitonesPerOctave];
        foreach (var partial in partials)
            for (int pc = 0; pc < SemitonesPerOctave; pc++) chroma[pc] += partial[pc];

        return chroma;
    }
}
