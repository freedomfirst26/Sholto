using Sholto.App.Audio;

namespace Sholto.Host;

/// <summary>Builds the loop options.</summary>
public interface ILoopOptionsFactory
{
    LoopOptions Create();
}
