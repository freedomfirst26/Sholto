namespace Sholto.Storage;

public sealed class IncompatibleSchemaException : Exception
{
    public long UserVersion { get; }
    public IncompatibleSchemaException(long userVersion, string message) : base(message)
    {
        UserVersion = userVersion;
    }
}
