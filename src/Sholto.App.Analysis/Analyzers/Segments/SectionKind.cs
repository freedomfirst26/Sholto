namespace Sholto.App.Analysis.Analyzers.Segments;

/// <summary>The structural label of a <see cref="SongSection"/>. <c>Build</c> names the build
/// phase, and <see cref="Section"/> is the neutral label for songs without drops.</summary>
public enum SectionKind
{
    Intro,
    Build,
    Drop,
    Breakdown,
    Verse,
    Chorus,
    Bridge,
    Outro,
    /// <summary>Neutral section in a song with no drops.</summary>
    Section,
}
