using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Harmony;

namespace Sholto.App.Storage;

/// <summary>
/// Tiny binary serializer for <see cref="KeyAnalysis"/>. The record holds one
/// short string so JSON would be fine, but we keep the on-disk format
/// consistent with the rest of the analyses table (length-prefixed UTF-8).
/// Layout (little-endian, version 1):
///   u32 version (=1)
///   str (unused)  — BinaryWriter length-prefixed UTF-8; the old note-name slot,
///                  kept empty for format compatibility; ignored on read
///   str camelot — BinaryWriter length-prefixed UTF-8; Camelot rendering of the
///                  key ("" if absent)
/// </summary>
internal sealed class KeyAnalysisSerializer(IKeyFactory keyFactory) : IKeyAnalysisSerializer
{
    public const uint Version = 1;

    private readonly IKeyFactory _keyFactory = keyFactory;

    public byte[] Encode(KeyAnalysis a)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(Version);
        w.Write("");
        w.Write(a.Key?.ToCamelot() ?? "");
        return ms.ToArray();
    }

    public KeyAnalysis? Decode(byte[] blob)
    {
        if (blob.Length < 4) return null;
        using var ms = new MemoryStream(blob);
        using var r = new BinaryReader(ms);
        if (r.ReadUInt32() != Version) return null;
        r.ReadString(); // old note-name slot, discarded
        var camelot = r.ReadString();
        Key? key = _keyFactory.TryFromCamelot(camelot, out var k) ? k : null;
        return new KeyAnalysis(key);
    }
}
