namespace Sholto.App.Analysis.Analyzers.Complement;

/// <summary>One candidate as ranked by <see cref="ComplementaryRanker"/>.</summary>
/// <param name="Candidate">The candidate section, as handed in.</param>
/// <param name="Score">How well it complements the playing section, 0 (does not fit) to 1 (ideal).</param>
public readonly record struct RankedComplement(ComplementCandidate Candidate, double Score);
