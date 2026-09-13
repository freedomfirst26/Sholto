namespace Sholto.Bench.Appearance;

/// <summary>
/// What Bench saw: the result of one <c>CaptureRenderedFrame</c> written to a
/// PNG, for the <c>screenshot</c> subcommand and the scenario "screenshot"
/// action. Named for the sense it carries, alongside <c>Sholto.Bench.Sound</c>
/// (hearing) and <c>Sholto.Bench.Behaviour</c> (interaction) — "it wrote a PNG"
/// isn't a finding, "a 1200x900 non-blank PNG at &lt;path&gt;" is.
/// </summary>
public sealed class AppearanceCapture
{
    public required string Path { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public long FileSizeBytes { get; init; }

    /// <summary>True when every sampled pixel byte in the captured frame is
    /// identical — a single flat colour (or fully transparent), which is what a
    /// harness silently failing to render anything looks like. Not a proof the
    /// UI painted correctly, only a cheap check that it painted <i>something</i>.</summary>
    public bool IsBlank { get; init; }
}
