namespace Sholto.Host.Tests;

/// <summary>An <see cref="IExternalToolFinderFactory"/> whose finder knows exactly the tools
/// it is given (each "installed" at /fake/bin/&lt;name&gt;) and nothing else, whatever is on
/// this machine's PATH.</summary>
internal sealed class FixedToolFinderFactory(params string[] installed) : IExternalToolFinderFactory
{
    private readonly string[] _installed = installed;

    public IExternalToolFinder Create(ExternalToolOptions options) => new FixedToolFinder(_installed);

    private sealed class FixedToolFinder(string[] installed) : IExternalToolFinder
    {
        private readonly string[] _installed = installed;

        public string? Locate(string name) => _installed.Contains(name) ? "/fake/bin/" + name : null;
    }
}
