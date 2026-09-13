using Sholto.Audio;
using SfEngine = SoundFlow.Abstracts.AudioEngine;
using SoundFlow.Structs;

namespace Sholto.Bench.Rendering;

/// <summary>
/// Incremental sibling of <see cref="OfflineRenderer.Render"/>/<see cref="OfflineRenderer.RenderScenario"/>:
/// those two own the router and the WAV file for their own single call and
/// only "wait" (scenario) or a fixed duration (Render) produces output.
/// A golden-audio regression test needs more than that vocabulary — set an
/// EQ band, render some frames, sweep the filter, render more, toggle the
/// echo mid-stream, scratch the platter — all direct calls on the same
/// <see cref="Deck"/> objects the caller already built via
/// <see cref="BenchDeck.Create"/>. This type exposes exactly the
/// router-plus-writer session those two methods already build internally, so
/// a caller can hold it open and mutate its own decks between
/// <see cref="Advance"/> calls, while every frame still comes from the same
/// <c>CueOutputRouter.Process</c> per-buffer pull the real audio device
/// callback makes (see <see cref="OfflineRenderer"/>'s class doc).
/// </summary>
public sealed class RenderSession : IDisposable
{
    private readonly CueOutputRouter _router;
    private readonly WavWriter _writer;
    private readonly int _sampleRate;
    private readonly int _channels;
    private bool _disposed;

    public RenderSession(IReadOnlyList<IMixSource> sources, SfEngine engine, AudioFormat deviceFormat, string outWavPath)
    {
        _router = new CueOutputRouter(engine, deviceFormat, sources);
        _writer = new WavWriter(outWavPath, deviceFormat.SampleRate, deviceFormat.Channels);
        _sampleRate = deviceFormat.SampleRate;
        _channels = deviceFormat.Channels;
    }

    /// <summary>Render exactly <paramref name="span"/> worth of frames at
    /// whatever deck/effect state the caller has set right now, and append
    /// them to the WAV. Call any Deck mutator (Volume, SetEq, SetFilter,
    /// SetEcho, ScratchRate/EndScratch, ...) between calls to script a
    /// timeline finer than the load/gain/play/crossfader/wait scenario
    /// vocabulary allows.</summary>
    public void Advance(TimeSpan span)
    {
        long frames = (long)(span.TotalSeconds * _sampleRate);
        OfflineRenderer.RenderFrames(_router, _writer, frames, _channels);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _writer.Dispose();
    }
}
