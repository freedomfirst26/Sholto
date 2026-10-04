namespace Sholto.App.Analysis.Harmony;

/// <summary>
/// Harmonic compatibility class between two Camelot keys, mirroring how
/// Rekordbox / Mixed In Key visualise the wheel:
///   Perfect   — same key (also the relative major/minor — same number,
///               opposite letter): seamless mix
///   Close     — ±1 step on the wheel, same letter: classic perfect-4th / 5th move
///   EnergyBoost — diagonal +7 on the same letter (energy lift)
///   Far       — everything else: probably clashes
/// </summary>
public enum HarmonicMatchResult { Perfect, Close, EnergyBoost, Far }
