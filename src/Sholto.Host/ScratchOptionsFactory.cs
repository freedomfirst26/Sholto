using Sholto.App;
namespace Sholto.Host;

/// <summary>Builds the <see cref="ScratchOptions"/>: the defaults, with <see cref="ScratchOptions.Log"/> on
/// only when the <c>SHOLTO_SCRATCH_LOG</c> environment variable is exactly <c>1</c>.
/// Called once, from <see cref="SholtoOptions"/>.</summary>
public sealed class ScratchOptionsFactory(Func<string, string?> env)
{
    private readonly Func<string, string?> _env = env;

    public ScratchOptions Create() => new() { Log = _env("SHOLTO_SCRATCH_LOG") == "1" };
}
