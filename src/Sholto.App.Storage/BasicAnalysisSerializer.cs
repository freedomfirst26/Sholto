using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.Data;

namespace Sholto.App.Storage;

/// <summary>
/// Compact binary serialization for BasicAnalysis. Each float array is written
/// as a little-endian 4-byte payload (no JSON overhead) — about 4× smaller and
/// 10× faster than JSON on a 6-minute track.
/// Layout (little-endian, version 1):
///   u32 version (=1)
///   f64 bpm
///   i32 samplesPerPeak
///   i32 peakCount       then min[peakCount] max[] low[] mid[] high[] as f32
///   i32 beatsCount      then beats[] as f64
///   i32 downbeatsCount  then downbeats[] as f64
/// </summary>
internal sealed class BasicAnalysisSerializer : IBasicAnalysisSerializer
{
    // v2: BPM from the mean (not median) of madmom's inter-beat gaps — the median
    //     biased quantized beat times high (174 DnB read 176.5).
    // v3: beatgrid phase anchored to the OPENING downbeats, not a whole-track
    //     average that madmom's wandering beats dragged ~a beat off at the start.
    // v4: waveform low/mid/high bands were identical for every track (filter state
    //     never advanced); re-analyse to bake real bands.
    // Bumping invalidates cached basic analyses so tracks re-derive the fixed BPM
    // + grid (and re-bake the calibrated waveform) on next load.
    public const uint Version = 4;

    public byte[] Encode(BasicAnalysis a)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(Version);
        w.Write(a.Bpm);
        w.Write(a.Peaks.SamplesPerPeak);
        WriteFloats(w, a.Peaks.Min);
        WriteFloats(w, a.Peaks.Max);
        WriteFloats(w, a.Peaks.Low);
        WriteFloats(w, a.Peaks.Mid);
        WriteFloats(w, a.Peaks.High);
        WriteDoubles(w, a.BeatTimes);
        WriteDoubles(w, a.DownbeatTimes ?? []);
        return ms.ToArray();
    }

    public BasicAnalysis? Decode(byte[] blob)
    {
        if (blob.Length < 8) return null;
        using var ms = new MemoryStream(blob);
        using var r = new BinaryReader(ms);
        uint version = r.ReadUInt32();
        if (version != Version) return null;

        double bpm = r.ReadDouble();
        int spp = r.ReadInt32();
        var min  = ReadFloats(r);
        var max  = ReadFloats(r);
        var low  = ReadFloats(r);
        var mid  = ReadFloats(r);
        var high = ReadFloats(r);
        var beats = ReadDoubles(r);
        var downs = ReadDoubles(r);
        var peaks = new WaveformPeaks(min, max, low, mid, high, spp, EngineAudio.SampleRate);
        return new BasicAnalysis(peaks, bpm, beats, downs);
    }

    private void WriteFloats(BinaryWriter w, float[] arr)
    {
        w.Write(arr.Length);
        for (int i = 0; i < arr.Length; i++) w.Write(arr[i]);
    }
    private float[] ReadFloats(BinaryReader r)
    {
        int n = r.ReadInt32();
        var a = new float[n];
        for (int i = 0; i < n; i++) a[i] = r.ReadSingle();
        return a;
    }
    private void WriteDoubles(BinaryWriter w, double[] arr)
    {
        w.Write(arr.Length);
        for (int i = 0; i < arr.Length; i++) w.Write(arr[i]);
    }
    private double[] ReadDoubles(BinaryReader r)
    {
        int n = r.ReadInt32();
        var a = new double[n];
        for (int i = 0; i < n; i++) a[i] = r.ReadDouble();
        return a;
    }
}
