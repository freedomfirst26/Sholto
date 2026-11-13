using SoundFlow.Abstracts;
using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.App.Audio.Tests.Recording;

/// <summary>An <see cref="IMixSource"/> at full master gain, cued, whose samples count up so every sample differs.
/// Unity component gain, so the router pulls exactly what it generates.</summary>
internal sealed class RampSource : SoundComponent, IMixSource
{
    private const float Step = 1e-5f;
    private const int Period = 100_000;

    private int _next;

    public RampSource(SfEngine engine, AudioFormat format)
        : base(engine, format)
    {
        Volume = UnityGain.CentrePanVolume;
    }

    public override string Name { get; set; } = "RampSource";

    public SoundComponent Component => this;

    public bool CueActive => true;

    public float MasterGain => 1f;

    protected override void GenerateAudio(Span<float> buffer, int channels)
    {
        for (int i = 0; i < buffer.Length; i++) buffer[i] = (_next++ % Period) * Step;
    }
}
