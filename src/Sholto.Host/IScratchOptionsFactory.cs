using Sholto.App;

namespace Sholto.Host;

/// <summary>Builds the scratch options.</summary>
public interface IScratchOptionsFactory
{
    ScratchOptions Create();
}
