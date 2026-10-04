using Sholto.App.Audio;

namespace Sholto.TestSupport;

/// <summary>An <see cref="IDeckFactory"/> that hands out the one set of ports it was given, so a test can
/// build a session over ports it controls.</summary>
internal sealed class FixedDeckFactory(IDeckPorts ports) : IDeckFactory
{
    private readonly IDeckPorts _ports = ports;

    public IDeckPorts Create() => _ports;
}
