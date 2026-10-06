using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stems;
using Sholto.App.Analysis.Stores;
using Sholto.Data;

namespace Sholto.App.Audio.Tests;

/// <summary>A <see cref="TrackAnalysisRun"/> wired the way <c>Deck</c> wires it, with every outcome recorded.
/// <see cref="Load"/> does what <c>TrackLoading.Load</c> does to the run: assign a fresh
/// <see cref="TrackAnalysis"/>, then kick off analysis.</summary>
internal sealed class TrackAnalysisRunRig
{
    public GatedAnalysisPipeline Pipeline { get; } = new();
    public List<BasicAnalysis> DetectedBasic { get; } = [];
    public List<StemSamples> StemsReady { get; } = [];
    public QueuedAppThread AppThread { get; } = new();
    public List<bool> BasicOnAppThread { get; } = [];
    public List<bool> StemsOnAppThread { get; } = [];
    public TrackAnalysisRun Run { get; }

    /// <param name="stemsOverlap">True for demucs confirmed on CUDA; false (CPU) keeps stems serial after basic.</param>
    public TrackAnalysisRunRig(bool stemsOverlap = false)
    {
        Pipeline.CanOverlap = stemsOverlap;
        Run = new TrackAnalysisRun(
            analysisProvider: Pipeline,
            keyCache: new NullKeyAnalysisStore(),
            keyAnalyzer: Pipeline,
            stems: Pipeline,
            reporter: new AnalysisReporter(Array.Empty<string>()),
            decoder: new SilentDecoder(),
            appThread: AppThread,
            setDetectedBasic: b => { DetectedBasic.Add(b); BasicOnAppThread.Add(AppThread.IsCurrent); },
            setSampleCount: _ => { },
            onStemsReady: s => { StemsReady.Add(s); StemsOnAppThread.Add(AppThread.IsCurrent); });
    }

    /// <summary>What <c>TrackLoading.BeginLoad</c> does to the run: a fresh <see cref="TrackAnalysis"/>, then
    /// pre-start the path-only analysis.</summary>
    public void BeginLoad(string path)
    {
        Run.Analysis = new TrackAnalysis();
        Run.Prestart(path);
    }

    public void Load(string path)
    {
        // BeginLoad already gave this track a fresh Analysis and pre-started its analysis; superseding
        // here would kill that work. Mirrors TrackLoading.Load.
        if (!Run.HasPrestartFor(path)) Run.Analysis = new TrackAnalysis();
        Run.KickOffAnalysis(new DecodedTrack(path, new float[8], 48000, 2));
    }

    /// <summary>Run what the analysis posted to the app thread, on the calling (test) thread.</summary>
    public void Drain() => AppThread.Drain();

    /// <summary>Give an in-flight run time to apply whatever it is going to apply.</summary>
    public async Task Settle()
    {
        await Task.Delay(300);
        Drain();
    }
}
