using Sholto.App.ExternalTools;

namespace Sholto.Interface.MainUI;

/// <summary>Builds the <see cref="IExternalToolFinder"/> over the per-run tool options,
/// which only <see cref="ExternalToolStackFactory.Build"/> has.</summary>
public interface IExternalToolFinderFactory
{
    IExternalToolFinder Create(ExternalToolOptions options);
}
