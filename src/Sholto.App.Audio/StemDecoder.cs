using Sholto.App.Analysis.Stems;

namespace Sholto.App.Audio;

/// <summary>
/// Decodes the four separated stem files into <see cref="StemSamples"/>, in parallel.
/// Implements the <see cref="IStemDecoder"/> port, which lives in <c>Sholto.App.Analysis</c>
/// because that project may not reference this one.
/// </summary>
public sealed class StemDecoder(IAudioFileDecoder decoder) : IStemDecoder
{
    private readonly IAudioFileDecoder _decoder = decoder;

    /// <inheritdoc/>
    public async Task<StemSamples> DecodeAsync(StemPaths paths, CancellationToken ct = default)
    {
        // Single-track Decode is ~1-3 s on a 4-minute MP3, so doing them serially
        // was a ~5x multiplier on stem load. Task.Run lets the thread pool fan them
        // out across cores; WhenAll joins back when the slowest finishes.
        var dT = Task.Run(() => _decoder.Decode(paths.Drums));
        var vT = Task.Run(() => _decoder.Decode(paths.Vocals));
        var bT = Task.Run(() => _decoder.Decode(paths.Bass));
        var oT = Task.Run(() => _decoder.Decode(paths.Other));
        await Task.WhenAll(dT, vT, bT, oT);
        return new StemSamples(dT.Result, vT.Result, bT.Result, oT.Result);
    }
}
