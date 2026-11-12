using System.Reflection;

namespace Sholto.Host;

/// <summary>The informational version of an assembly (the entry assembly in the app), or <c>unknown</c> when
/// there is no assembly or it carries none.</summary>
public sealed class CrashAppVersion(Assembly? assembly) : ICrashAppVersion
{
    private const string Unknown = "unknown";

    private readonly string _version =
        assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion is { Length: > 0 } v
            ? v
            : Unknown;

    public string Version => _version;
}
