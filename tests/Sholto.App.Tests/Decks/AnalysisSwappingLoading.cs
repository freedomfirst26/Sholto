using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Audio;
using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.App.Tests;

/// <summary>Track loading that hands out a fresh <see cref="TrackAnalysis"/> after every load and unload, as
/// the real player does (the scripted loading never swaps), so a test can see what a session does with the
/// previous track's analysis. Everything else is the inner loading's.</summary>
internal sealed class AnalysisSwappingLoading(ITrackLoading inner) : ITrackLoading
{
    private readonly ITrackLoading _inner = inner;

    public TrackAnalysis Analysis { get; private set; } = inner.Analysis;

    public IAnalysisProvider AnalysisProvider => _inner.AnalysisProvider;

    public IAnalysisReporter Reporter => _inner.Reporter;

    public bool IsLoaded => _inner.IsLoaded;

    public bool IsPlaying => _inner.IsPlaying;

    public event Action? AnalysisUpdated
    {
        add => _inner.AnalysisUpdated += value;
        remove => _inner.AnalysisUpdated -= value;
    }

    public void BeginLoad(string filePath)
    {
        _inner.BeginLoad(filePath);
        Analysis = new TrackAnalysis();
    }

    public void LoadStreaming(string filePath)
    {
        _inner.LoadStreaming(filePath);
        Analysis = new TrackAnalysis();
    }

    public void Load(string filePath, float[] stereoSamples, int sampleRate)
    {
        _inner.Load(filePath, stereoSamples, sampleRate);
        Analysis = new TrackAnalysis();
    }

    public void Unload()
    {
        _inner.Unload();
        Analysis = new TrackAnalysis();
    }

    public void AttachEngine(SfEngine engine, AudioFormat format) => _inner.AttachEngine(engine, format);
}
