using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Harmony;
using Sholto.Data;

namespace Sholto.App.Glance;

/// <inheritdoc cref="IFitScorer"/>
public sealed class FitScorer(IOptions<GlanceOptions> options) : IFitScorer
{
    private readonly GlanceOptions _options = options.Value;

    public FitResult Score(TrackSummary candidate, GlanceReference reference)
    {
        double? bpm = candidate.Bpm is { } raw && raw > 0 ? raw * candidate.BpmMultiplier : null;
        double? refBpm = reference.Bpm is { } r && r > 0 ? r : null;

        double? pct = null;
        if (bpm is { } b && refBpm is { } rb)
            pct = ClosestPercent(b, rb);

        if (candidate.IsAnalyzing || candidate.MusicalKey is null || reference.Key is null || pct is null)
            return new FitResult(FitLevel.None, -1, candidate.IsAnalyzing ? null : pct);

        double a = Math.Abs(pct.Value);
        int tempoScore = a <= _options.GoodTempoPercent ? 2 : a <= _options.UsableTempoPercent ? 1 : 0;
        int score = KeyScore(reference.Key.Value, candidate.MusicalKey.Value) + tempoScore;

        if (a > _options.ClashTempoPercent)
            return new FitResult(FitLevel.Clash, Math.Min(score, 1), pct);

        FitLevel level = score >= 4 ? FitLevel.Good : score == 3 ? FitLevel.Usable : FitLevel.Clash;
        return new FitResult(level, score, pct);
    }

    private double ClosestPercent(double bpm, double reference)
    {
        double best = (bpm - reference) / reference * 100.0;
        double up = (bpm * 2 - reference) / reference * 100.0;
        double down = (bpm / 2 - reference) / reference * 100.0;
        if (Math.Abs(up) < Math.Abs(best)) best = up;
        if (Math.Abs(down) < Math.Abs(best)) best = down;
        return best;
    }

    private int KeyScore(KeyRef reference, KeyRef candidate)
    {
        var a = new Key(reference.PitchClass, reference.IsMajor);
        var b = new Key(candidate.PitchClass, candidate.IsMajor);
        int na = a.CamelotNumber;
        int nb = b.CamelotNumber;
        if (na == nb)
            return a.IsMajor == b.IsMajor ? 3 : 2;
        if (a.IsMajor != b.IsMajor)
            return 0;
        int d = Math.Min((na - nb + 12) % 12, (nb - na + 12) % 12);
        return d == 1 ? 2 : d == 2 ? 1 : 0;
    }
}
