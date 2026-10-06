using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Audio;
using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.TestSupport;

/// <summary>Track loading with no audio behind it: the load calls do nothing, the analysis is the inner
/// deck's own bag (so a test can pre-fill it), and <see cref="IsPlaying"/> is whatever the test says.</summary>
internal sealed class ScriptedLoading(ITrackLoading inner, IAnalysisProvider? analysisProvider = null) : ITrackLoading
{
    private readonly ITrackLoading _inner = inner;
    private readonly IAnalysisProvider? _analysisProvider = analysisProvider;

    public bool IsPlaying { get; set; }

    public bool IsLoaded { get; set; }

    public TrackAnalysis Analysis => _inner.Analysis;

    public IAnalysisProvider AnalysisProvider => _analysisProvider ?? _inner.AnalysisProvider;

    public IAnalysisReporter Reporter => _inner.Reporter;

    public event Action? AnalysisUpdated
    {
        add { }
        remove { }
    }

    public void BeginLoad(string filePath)
    {
    }

    public void LoadStreaming(string filePath) => IsLoaded = true;

    public void Load(string filePath, float[] stereoSamples, int sampleRate) => IsLoaded = true;

    public void Unload()
    {
        IsLoaded = false;
        IsPlaying = false;
    }

    public void AttachEngine(SfEngine engine, AudioFormat format) => _inner.AttachEngine(engine, format);
}
