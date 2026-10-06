using Sholto.App.Lifecycle;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Stages;

namespace Sholto.Host;

/// <summary>Builds the <see cref="AnalysisStack"/> around the beat step, with its persistent
/// stores deferred until <see cref="ILibraryDatabase.Opened"/>.</summary>
public interface IAnalysisStackFactory
{
    AnalysisStack Build(IBeatAnalysisStep beats, ILibraryDatabase libraryDatabase);
}
