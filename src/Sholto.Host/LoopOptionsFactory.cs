using Sholto.App.Audio;
using Sholto.Data;

namespace Sholto.Host;

/// <summary>Builds the <see cref="LoopOptions"/>: the defaults, overridden by the
/// <c>SHOLTO_LOOP_MODE</c> and <c>SHOLTO_LOOP_FADE_MS</c> environment variables (A/B testing).
/// Called once, from <see cref="SholtoOptions"/>; a fade override is reported on the <c>StemMix</c> log.</summary>
public sealed class LoopOptionsFactory(Func<string, string?> env, ILogFactory logs) : ILoopOptionsFactory
{
    private readonly Func<string, string?> _env = env;
    private readonly ILog _log = logs.Create("StemMix");

    public LoopOptions Create()
    {
        var options = new LoopOptions();
        var mode = _env("SHOLTO_LOOP_MODE");
        if (mode is not null) options.Mode = mode;

        var s = _env("SHOLTO_LOOP_FADE_MS");
        if (s is not null && double.TryParse(s, out double ms) && ms > 0.0 && ms < 100.0)
        {
            options.FadeMs = Math.Max(ms, 0.5);
            _log.Info($"loop fade overridden via SHOLTO_LOOP_FADE_MS={ms}");
        }
        return options;
    }
}
