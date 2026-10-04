namespace Sholto.App.Analysis.Stems;

/// <summary>
/// Port: decodes the four separated stem WAVs into sample buffers. Implemented in
/// <c>Sholto.App.Audio</c>, which this project may not reference.
/// </summary>
public interface IStemDecoder
{
    Task<StemSamples> DecodeAsync(StemPaths paths, CancellationToken ct = default);
}
