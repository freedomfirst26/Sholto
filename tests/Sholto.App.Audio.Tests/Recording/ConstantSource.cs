using SoundFlow.Abstracts;
using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.App.Audio.Tests.Recording;

/// <summary>An <see cref="IMixSource"/> whose stereo is one constant level on both channels, at unity component gain
/// so the router pulls exactly that level.</summary>
internal sealed class ConstantSource : SoundComponent, IMixSource
{
    private readonly float _level;

    public ConstantSource(SfEngine engine, AudioFormat format, float level, float masterGain, bool cueActive)
        : base(engine, format)
    {
        _level = level;
        MasterGain = masterGain;
        CueActive = cueActive;
        Volume = UnityGain.CentrePanVolume;
    }

    public override string Name { get; set; } = "ConstantSource";

    public SoundComponent Component => this;

    public bool CueActive { get; }

    public float MasterGain { get; }

    protected override void GenerateAudio(Span<float> buffer, int channels) => buffer.Fill(_level);
}
