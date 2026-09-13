namespace Sholto.Audio;

/// <summary>
/// Builds a fully-formed <see cref="Deck"/> in one call: decoder, stem
/// analyzer, loop debug, reporter, analysis provider and the persistence-
/// cache delegates are all wired before <see cref="Create"/> returns.
///
/// Before this port existed, a Deck was built by MainViewModel (decoder /
/// stem analyzer / loop debug, plus Reporter) and then finished later, once
/// the app's DB had opened, by App.axaml.cs reaching back into
/// MainViewModel.Deck1.Player / Deck2.Player to set AnalysisProvider and the
/// four cache delegates. In between, a Deck existed half-built — null
/// AnalysisProvider, null caches — and a track loading during that window
/// would hit Deck's own "AnalysisProvider must be set" guard.
///
/// Implementations must never hand back a Deck in that state. See
/// <see cref="DeckFactory"/> for how it does that without forcing the DB to
/// exist any earlier than it does today.
/// </summary>
public interface IDeckFactory
{
    /// <summary>Build one fully-wired <see cref="Deck"/>. MainViewModel calls
    /// this once per deck (twice total).</summary>
    Deck Create();
}
