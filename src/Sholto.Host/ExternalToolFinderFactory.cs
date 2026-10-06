using Sholto.App.ExternalTools;

namespace Sholto.Host;

/// <summary>Builds the real <see cref="ExternalToolFinder"/>.</summary>
public sealed class ExternalToolFinderFactory : IExternalToolFinderFactory
{
    public IExternalToolFinder Create(ExternalToolOptions options) => new ExternalToolFinder(options);
}
