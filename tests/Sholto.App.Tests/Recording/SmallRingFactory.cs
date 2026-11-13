using Sholto.App.Audio.Recording;

namespace Sholto.App.Tests.Recording;

/// <summary>A real ring, but a small one.</summary>
internal sealed class SmallRingFactory : IStereoSampleRingFactory
{
    public IStereoSampleRing Create() => new StereoSampleRing(16);
}
