using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Analysis.Harmony;

namespace Sholto.App.Analysis.Analyzers.Complement;

/// <summary>What <see cref="ComplementaryRanker"/> knows about one song section: its track's key
/// and tempo, the section's role, and how loud it is. Used both for the playing section and for
/// each candidate section from another track.</summary>
/// <param name="Key">The track's key.</param>
/// <param name="Bpm">The track's tempo in beats per minute; zero or less means unknown.</param>
/// <param name="Kind">The section's structural label.</param>
/// <param name="MeanLoudness">The section's mean per-bar loudness (<see cref="BarFeatures.Loudness"/>),
/// relative to its own track's sustained maximum, so roughly 0..1.</param>
public sealed record ComplementCandidate(Key Key, double Bpm, SectionKind Kind, float MeanLoudness);
