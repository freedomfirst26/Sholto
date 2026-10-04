namespace Sholto.App.Library.Tags;

/// <summary>Turns a user-typed tag name into its canonical form, or null if it is unusable.</summary>
public interface ITagNameNormalizer
{
    string? Normalize(string? raw);
}
