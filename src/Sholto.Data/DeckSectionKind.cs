namespace Sholto.Data;

/// <summary>The structural label of a <see cref="DeckSection"/>. The bus-side twin of the App's
/// section kind: Sholto.Data knows no App types, so the publisher maps one to the other by name.</summary>
public enum DeckSectionKind
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
