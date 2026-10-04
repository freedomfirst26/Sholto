using Sholto.App.Analysis.Analyzers;

namespace Sholto.App.Analysis.Analyzers.Vocals;

/// <summary>One span of a track where the vocal is present, in track seconds.</summary>
public readonly record struct VocalRegion(double StartSec, double EndSec);
