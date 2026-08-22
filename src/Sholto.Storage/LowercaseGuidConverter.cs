using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Sholto.Storage;

/// <summary>
/// Maps every <see cref="Guid"/> key/FK to lowercase "D" text.
///
/// <para><b>Why.</b> The library's Guid keys are stored as lowercase text (the
/// pre-EF bridge layer wrote them that way), but Microsoft.Data.Sqlite's default
/// Guid-parameter encoding is UPPERCASE text. SQLite compares TEXT with the
/// case-sensitive BINARY collation, so EF equality (<c>t.Id == id</c>) and
/// foreign-key inserts silently matched nothing — analysis back-fills failed the
/// FK check and were swallowed, so a re-analysed track never persisted and was
/// recomputed on every launch. Pinning EF to lowercase makes reads, writes, and
/// FKs agree with the existing rows and with each other.</para>
/// </summary>
public sealed class LowercaseGuidConverter : ValueConverter<Guid, string>
{
    public LowercaseGuidConverter()
        : base(g => g.ToString("D"), s => Guid.Parse(s)) { }
}
