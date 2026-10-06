using Sholto.Data;

namespace Sholto.App.Glance;

/// <summary>The deck a Glance search is measured against: the deck that is not the load target.</summary>
/// <param name="Deck">The reference deck index.</param>
/// <param name="Key">The reference deck's loaded key, if known.</param>
/// <param name="Bpm">The reference deck's effective BPM, if known.</param>
/// <param name="FilePath">The file loaded in the reference deck, so its row can be marked.</param>
public readonly record struct GlanceReference(int Deck, KeyRef? Key, double? Bpm, string? FilePath);
