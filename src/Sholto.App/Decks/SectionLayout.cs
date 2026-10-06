using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Analyzers.Segments;

namespace Sholto.App.Decks;

/// <summary>The bar grid a track's sections were computed on, with the phrase grid and the sections
/// themselves.</summary>
public sealed record SectionLayout(Beatgrid Grid, PhraseGrid Phrases, IReadOnlyList<SongSection> Sections);
