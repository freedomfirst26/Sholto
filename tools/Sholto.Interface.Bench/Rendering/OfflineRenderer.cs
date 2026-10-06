using Sholto.App.Audio;
using Sholto.App.Dsp;
using SoundComponent = SoundFlow.Abstracts.SoundComponent;
using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.Interface.Bench.Rendering;

/// <summary>
/// Renders <see cref="CueOutputRouter"/> — the device's single source — to a
/// WAV file with no audio device involved. It pulls each source's post-EQ
/// stereo once per buffer via the public <c>SoundComponent.Process</c>, exactly
/// as the real device callback does; this loop just calls it in a tight loop
/// instead of from miniaudio's callback thread. Deterministic, and as fast as
/// the CPU can decode/mix — not real time.
/// </summary>
/// <param name="benchDeck">Builds the offline engine and decks a scenario render runs against.</param>
/// <param name="crossfade">The crossfade curve the scenario runner applies.</param>
/// <param name="routerFactory">Builds the router each render pulls from.</param>
public sealed class OfflineRenderer(IBenchDeck benchDeck, ICrossfadeCurve crossfade, ICueOutputRouterFactory routerFactory) : IOfflineRenderer
{
    /// <summary>Frames pulled per <c>Process</c> call. Arbitrary — large enough
    /// to keep the loop count sane, small enough to bound scratch-buffer size.
    /// Shared with <see cref="DeckAdvance"/>'s own pull loop.</summary>
    internal const int FramesPerBuffer = 4096;

    public void Render(
        IReadOnlyList<IMixSource> sources,
        SfEngine engine,
        AudioFormat deviceFormat,
        TimeSpan duration,
        string outWavPath)
    {
        var router = routerFactory.Create(engine, deviceFormat, sources);
        using var writer = new WavWriter(outWavPath, deviceFormat.SampleRate, deviceFormat.Channels);
        long totalFrames = (long)(duration.TotalSeconds * deviceFormat.SampleRate);
        RenderFrames(router, writer, totalFrames, deviceFormat.Channels);
    }

    /// <summary>Pulls exactly <paramref name="frameCount"/> frames from
    /// <paramref name="router"/> and appends them to <paramref name="writer"/>.
    /// Factored out of <see cref="Render"/> so a scenario's "wait N seconds" step
    /// can render incrementally between other steps (load/gain/crossfader changes
    /// take effect between calls) instead of only ever rendering one fixed span
    /// up front.</summary>
    public void RenderFrames(SoundComponent router, WavWriter writer, long frameCount, int channels)
    {
        var buffer = new float[FramesPerBuffer * channels];
        long framesWritten = 0;
        while (framesWritten < frameCount)
        {
            int frames = (int)Math.Min(FramesPerBuffer, frameCount - framesWritten);
            var span = buffer.AsSpan(0, frames * channels);
            // The router ADDS its mix into the span, so a reused buffer must be
            // zeroed first or every buffer is a running sum of all before it.
            span.Clear();
            router.Process(span, channels);
            writer.WriteInterleaved(span);
            framesWritten += frames;
        }
    }

    /// <summary>Runs a scenario end-to-end into a WAV: builds two bare decks on a
    /// fresh offline engine, wires them into a router, then lets
    /// <see cref="Sholto.Interface.Bench.Scenario.ScenarioRunner"/> drive load/gain/play/
    /// crossfader/wait against them — "wait" is the only step that actually
    /// produces output (via <see cref="RenderFrames"/>); everything else takes
    /// effect instantly, same as flipping a control between two renders.</summary>
    public void RenderScenario(Scenario.Scenario scenario, AudioFormat deviceFormat, string outWavPath)
    {
        var engine = benchDeck.CreateEngine();
        var deck1 = benchDeck.Create(engine);
        var deck2 = benchDeck.Create(engine);
        var router = routerFactory.Create(engine, deviceFormat, [deck1, deck2]);
        using var writer = new WavWriter(outWavPath, deviceFormat.SampleRate, deviceFormat.Channels);

        var runner = new Scenario.ScenarioRunner(
            new Dictionary<int, Deck> { [1] = deck1, [2] = deck2 },
            advance: span =>
            {
                long frames = (long)(span.TotalSeconds * deviceFormat.SampleRate);
                RenderFrames(router, writer, frames, deviceFormat.Channels);
            },
            crossfade);
        runner.Run(scenario);
    }
}
