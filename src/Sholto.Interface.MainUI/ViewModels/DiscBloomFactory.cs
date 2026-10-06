using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>Builds <see cref="DiscBloom"/>s that measure their frame gaps on the app's frame clock.</summary>
public sealed class DiscBloomFactory(IFrameClock clock) : IDiscBloomFactory
{
    private readonly IFrameClock _clock = clock;

    public IDiscBloom Create() => new DiscBloom(_clock);
}
