namespace Sholto.App.Lifecycle;

/// <summary>Turns the Track List's saved form to JSON text and back.</summary>
public interface ISavedTrackListCodec
{
    /// <summary>The JSON text for <paramref name="saved"/>.</summary>
    string Encode(SavedTrackList saved);

    /// <summary>The saved list in <paramref name="json"/>. Throws <see cref="System.Text.Json.JsonException"/>
    /// when the text is not a saved Track List.</summary>
    SavedTrackList Decode(string json);
}
