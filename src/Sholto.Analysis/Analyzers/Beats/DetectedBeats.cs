using Sholto.Analysis.Analyzers;

namespace Sholto.Analysis.Analyzers.Beats;

/// <summary>
/// Raw beat detections straight from the beat-tracking tool, BEFORE any grid fitting.
/// Distinct from <see cref="BasicAnalysis"/>, which holds the synthesised constant-spacing
/// grid produced from these by <see cref="BeatgridFitter.SynthesizeFullGrid"/> — same three field
/// names, different meaning. Passing one where the other is wanted is a real mistake, which
/// is why this type exists rather than a tuple.
///
/// The three values are not peers: one array of observations, a less-trustworthy subset of
/// it, and a statistic over it.
/// </summary>
/// <param name="Bpm">DERIVED, not detected — <c>Sholto.ExternalTools.MadmomTool.BpmFromBeats</c>
/// takes a trimmed MEAN of the gaps between <paramref name="BeatTimes"/> (see that method's
/// own comment for why mean, not median). Carries no information the other two fields do
/// not already contain.</param>
/// <param name="BeatTimes">The primary data: every beat found, in seconds, all four of each
/// bar. Quantised to the tool's ~10 ms frame grid, and may contain dropouts and doubled
/// detections — which is why a fitted grid is synthesised from it rather than used raw.</param>
/// <param name="DownbeatTimes">A SUBSET of <paramref name="BeatTimes"/> — the detections
/// labelled beat 1 of a bar (madmom emits <c>TIME\tBEAT_NUMBER</c>; these are the rows where
/// the number is 1). LESS TRUSTWORTHY than <paramref name="BeatTimes"/>: madmom mis-phases
/// downbeats, which is why auto-loop defaults to beat-snap rather than bar-snap.</param>
public sealed record DetectedBeats(double Bpm, double[] BeatTimes, double[] DownbeatTimes);
