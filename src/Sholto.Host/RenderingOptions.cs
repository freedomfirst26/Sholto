namespace Sholto.Host;

/// <summary>Rendering settings supplied via the standard <c>IOptions&lt;RenderingOptions&gt;</c>
/// pipeline. Defaults live here; a config source can override them later.</summary>
public sealed class RenderingOptions
{
    /// <summary>Ceiling, in bytes, of Skia's GPU resource cache. Avalonia's default is
    /// 28 MB, and Skia draws any raster image larger than half the cache tiled,
    /// re-uploading it on every draw. A deck's baked waveform is about 2.9 MB per
    /// minute of track, so a 6-minute track, or two decks together, were re-uploaded
    /// every frame (about 50 % of a core at 4K). This is a ceiling, not an allocation.</summary>
    public long MaxGpuResourceSizeBytes { get; set; } = 256L * 1024 * 1024;
}
