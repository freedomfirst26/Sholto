namespace Sholto.Storage.Entities;

/// <summary>What a marker is used for. Memory = a plain saved position (Rekordbox
/// "memory cue"); In/Out designate transition points that a <see cref="MarkerLink"/>
/// can chain across decks; Hot = a quick-jump performance cue.</summary>
internal enum MarkerKind
{
    Memory,
    In,
    Out,
    Hot,
}
