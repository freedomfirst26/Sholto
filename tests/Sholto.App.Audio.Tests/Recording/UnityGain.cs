namespace Sholto.App.Audio.Tests.Recording;

/// <summary>SoundFlow scales every component's output by its volume × an equal-power centre pan (√½ per channel) after
/// it generates. This volume cancels that, so a test component's output is exactly what it generated.</summary>
internal sealed class UnityGain
{
    public const float CentrePanVolume = 1.41421356f;
}
