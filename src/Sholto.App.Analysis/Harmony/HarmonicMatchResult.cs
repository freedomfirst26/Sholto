namespace Sholto.App.Analysis.Harmony;

/// <summary>
/// Harmonic compatibility class between two keys, judged on the Camelot wheel:
///   Perfect     — same Camelot number (the same key, or its relative major/minor): seamless mix
///   Close       — ±1 step on the wheel, same mode: classic perfect-4th / 5th move
///   EnergyBoost — ±1 step on the wheel, opposite mode (the diagonal)
///   Far         — everything else: probably clashes
/// </summary>
public enum HarmonicMatchResult { Perfect, Close, EnergyBoost, Far }
