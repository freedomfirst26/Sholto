using Sholto.App.Audio;
using SoundComponent = SoundFlow.Abstracts.SoundComponent;
using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.Interface.Bench.Rendering;

/// <summary>Renders decks to a WAV file with no audio device involved.</summary>
public interface IOfflineRenderer
{
    void Render(IReadOnlyList<IMixSource> sources, SfEngine engine, AudioFormat deviceFormat, TimeSpan duration, string outWavPath);

    void RenderScenario(Scenario.Scenario scenario, AudioFormat deviceFormat, string outWavPath);

    /// <summary>Pulls exactly <paramref name="frameCount"/> frames from
    /// <paramref name="router"/> (a <c>CueOutputRouter</c>) into <paramref name="writer"/>.</summary>
    void RenderFrames(SoundComponent router, WavWriter writer, long frameCount, int channels);
}
