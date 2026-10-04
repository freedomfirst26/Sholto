using Sholto.App.Analysis.Analyzers;

namespace Sholto.App.Analysis.Analyzers.Segments;

/// <summary>One contiguous section of a track, bar-aligned.</summary>
public readonly record struct SongSegment(double StartSec, double EndSec, SegmentKind Kind, float Energy);
