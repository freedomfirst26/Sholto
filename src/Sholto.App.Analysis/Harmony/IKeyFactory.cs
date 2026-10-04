namespace Sholto.App.Analysis.Harmony;

/// <summary>Builds <see cref="Key"/> values from their Camelot rendering ("8B", "11A").</summary>
public interface IKeyFactory
{
    /// <summary>Parse a Camelot code like "8B" / "11A" into a <see cref="Key"/>. Returns false if malformed.</summary>
    bool TryFromCamelot(string? code, out Key key);
}
