namespace Sholto.Data;

/// <summary>The capability labels an external tool can back, carried in <see cref="ToolStatus.Capability"/>.
/// Static because it holds only consts (forced).</summary>
public static class ToolCapabilities
{
    public const string Beats = "beats";
    public const string Stems = "stems";
    public const string Transcode = "transcode";
}
