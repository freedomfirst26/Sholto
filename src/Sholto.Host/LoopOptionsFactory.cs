using Sholto.App.Audio;
namespace Sholto.Host;

/// <summary>Builds the <see cref="LoopOptions"/>: the defaults, overridden by the
/// <c>SHOLTO_LOOP_MODE</c> and <c>SHOLTO_LOOP_FADE_MS</c> environment variables (A/B testing).
/// Called once, from <see cref="SholtoOptions"/>.</summary>
public sealed class LoopOptionsFactory : ILoopOptionsFactory
{
    public LoopOptions Create()
    {
        var options = new LoopOptions();
        var mode = Environment.GetEnvironmentVariable("SHOLTO_LOOP_MODE");
        if (mode is not null) options.Mode = mode;

        var s = Environment.GetEnvironmentVariable("SHOLTO_LOOP_FADE_MS");
        if (s is not null && double.TryParse(s, out double ms) && ms > 0.0 && ms < 100.0)
        {
            options.FadeMs = Math.Max(ms, 0.5);
            Console.WriteLine($"[StemMix] loop fade overridden via SHOLTO_LOOP_FADE_MS={ms}");
        }
        return options;
    }
}
