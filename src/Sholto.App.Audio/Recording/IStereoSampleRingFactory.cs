namespace Sholto.App.Audio.Recording;

/// <summary>Builds the ring one recording captures into.</summary>
public interface IStereoSampleRingFactory
{
    IStereoSampleRing Create();
}
