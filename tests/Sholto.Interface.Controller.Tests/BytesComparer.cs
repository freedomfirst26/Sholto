namespace Sholto.Interface.Controller.Tests;

/// <summary>Compares byte arrays by content.</summary>
public sealed class BytesComparer : IEqualityComparer<byte[]>
{
    public bool Equals(byte[]? x, byte[]? y) => x is not null && y is not null && x.AsSpan().SequenceEqual(y);

    public int GetHashCode(byte[] obj) => obj.Length;
}
