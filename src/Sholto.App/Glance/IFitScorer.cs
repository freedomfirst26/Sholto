using Sholto.Data;

namespace Sholto.App.Glance;

/// <summary>Scores how well a candidate track mixes with the reference deck (key on the Camelot wheel plus tempo).</summary>
public interface IFitScorer
{
    /// <summary>Score <paramref name="candidate"/> against <paramref name="reference"/>.</summary>
    FitResult Score(TrackSummary candidate, GlanceReference reference);
}
