namespace Sholto.App.Audio.Tests.Recording;

/// <summary>A file that refuses, with the disk-full <see cref="IOException"/>, any write that would grow it past
/// <c>capacityBytes</c>; rewrites inside that size (a WAV header) still land.</summary>
internal sealed class DiskFullStream(FileStream file, long capacityBytes) : Stream
{
    public const string DiskFullMessage = "No space left on device";

    private readonly FileStream _file = file;
    private readonly long _capacityBytes = capacityBytes;

    public override bool CanRead => false;
    public override bool CanSeek => true;
    public override bool CanWrite => true;
    public override long Length => _file.Length;

    public override long Position
    {
        get => _file.Position;
        set => _file.Position = value;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (_file.Position + count > _capacityBytes) throw new IOException(DiskFullMessage);
        _file.Write(buffer, offset, count);
    }

    public override void Flush() => _file.Flush();
    public override long Seek(long offset, SeekOrigin origin) => _file.Seek(offset, origin);
    public override void SetLength(long value) => _file.SetLength(value);
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _file.Dispose();
        base.Dispose(disposing);
    }
}
