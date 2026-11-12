using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Harmony;

namespace Sholto.App.Analysis.Analyzers.Complement;

/// <summary>Rule-based ranking of candidate sections from other tracks by how well each complements
/// the playing section on key, tempo, energy and section role. No model: every weight and threshold
/// is in <see cref="ComplementOptions"/>.</summary>
public sealed class ComplementaryRanker
{
    private readonly ComplementOptions _options;
    private readonly double _weightSum;

    public ComplementaryRanker(IOptions<ComplementOptions> options)
    {
        _options = options.Value;
        if (_options.KeyWeight < 0 || _options.TempoWeight < 0 || _options.EnergyWeight < 0 || _options.RoleWeight < 0)
            throw new ArgumentException("Complement weights must not be negative.", nameof(options));
        _weightSum = _options.KeyWeight + _options.TempoWeight + _options.EnergyWeight + _options.RoleWeight;
        if (_weightSum <= 0) throw new ArgumentException("At least one complement weight must be positive.", nameof(options));
        if (_options.TempoWindowPercent <= 0) throw new ArgumentException("The tempo window must be positive.", nameof(options));
        if (_options.EnergyGap <= 0) throw new ArgumentException("The energy gap must be positive.", nameof(options));
    }

    /// <summary>The candidates ordered best first, each with its score. Equal scores keep their input order.</summary>
    public IReadOnlyList<RankedComplement> Rank(ComplementCandidate playing, IReadOnlyList<ComplementCandidate> candidates) =>
        candidates
            .Select(candidate => new RankedComplement(candidate, Score(playing, candidate)))
            .OrderByDescending(ranked => ranked.Score) // OrderBy is stable: ties keep input order
            .ToList();

    /// <summary>How well <paramref name="candidate"/> complements <paramref name="playing"/>, 0..1.</summary>
    public double Score(ComplementCandidate playing, ComplementCandidate candidate)
    {
        var keyFit = KeyFit(playing.Key.Compatibility(candidate.Key));
        var tempoFit = TempoFit(playing.Bpm, candidate.Bpm);
        if (keyFit <= 0 || tempoFit <= 0) return 0;

        var weighted = _options.KeyWeight * keyFit
            + _options.TempoWeight * tempoFit
            + _options.EnergyWeight * EnergyFit(playing.MeanLoudness, candidate.MeanLoudness)
            + _options.RoleWeight * RoleFit(playing, candidate);
        return weighted / _weightSum;
    }

    private double KeyFit(HarmonicMatchResult match) => match switch
    {
        HarmonicMatchResult.Perfect => _options.PerfectKeyFit,
        HarmonicMatchResult.Close => _options.CloseKeyFit,
        HarmonicMatchResult.EnergyBoost => _options.EnergyBoostKeyFit,
        _ => 0,
    };

    private double TempoFit(double playingBpm, double candidateBpm)
    {
        if (playingBpm <= 0 || candidateBpm <= 0) return 0; // unknown tempo cannot be matched
        var gapPercent = Math.Abs(candidateBpm - playingBpm) / playingBpm * 100.0;
        return Math.Max(0, 1 - gapPercent / _options.TempoWindowPercent);
    }

    private double EnergyFit(float playingLoudness, float candidateLoudness)
    {
        var offIdeal = Math.Abs(candidateLoudness - playingLoudness - _options.EnergyLift);
        return Math.Max(0, 1 - offIdeal / _options.EnergyGap);
    }

    private double RoleFit(ComplementCandidate playing, ComplementCandidate candidate) =>
        _options.RolePairs.TryGetValue(playing.Kind, out var pairs) && pairs.Contains(candidate.Kind)
            ? _options.PairedRoleFit
            : _options.UnpairedRoleFit;
}
