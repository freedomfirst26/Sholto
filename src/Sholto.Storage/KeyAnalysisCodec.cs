using Sholto.Analysis;

namespace Sholto.Storage;

/// <summary>
/// Tiny binary serializer for <see cref="KeyAnalysis"/>. The record holds two
/// short strings so JSON would be fine, but we keep the on-disk format
/// consistent with the rest of the analyses table (length-prefixed UTF-8).
/// Layout (little-endian, version 1):
///   u32 version (=1)
///   str keyName  — BinaryWriter length-prefixed UTF-8
///   str camelot  — BinaryWriter length-prefixed UTF-8
/// </summary>
public static class KeyAnalysisCodec
{
    public const uint Version = 1;

    public static byte[] Encode(KeyAnalysis a)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(Version);
        w.Write(a.KeyName ?? "");
        w.Write(a.Camelot ?? "");
        return ms.ToArray();
    }

    public static KeyAnalysis? Decode(byte[] blob)
    {
        if (blob.Length < 4) return null;
        using var ms = new MemoryStream(blob);
        using var r = new BinaryReader(ms);
        if (r.ReadUInt32() != Version) return null;
        var keyName = r.ReadString();
        var camelot = r.ReadString();
        return new KeyAnalysis(keyName, camelot);
    }
}
