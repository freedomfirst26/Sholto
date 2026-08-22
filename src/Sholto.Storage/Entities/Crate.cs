namespace Sholto.Storage.Entities;

/// <summary>A named collection of tracks — the DJ "crate" (Serato's term for a
/// user-organised track group). Named <c>Crate</c> rather than <c>Set</c> because
/// <c>Set</c> collides with EF's <c>DbContext.Set&lt;T&gt;()</c>, and a live "set" is
/// a different concept. Many-to-many with tracks via <see cref="CrateTrack"/>, which
/// carries an ordering position so a crate is a sequenced list.</summary>
public sealed class Crate
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public ICollection<CrateTrack> CrateTracks { get; set; } = new List<CrateTrack>();
}
