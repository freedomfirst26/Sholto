using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sholto.App.Lifecycle;

/// <inheritdoc cref="ISavedTrackListCodec"/>
public sealed class SavedTrackListCodec : ISavedTrackListCodec
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public string Encode(SavedTrackList saved) => JsonSerializer.Serialize(saved, _options);

    public SavedTrackList Decode(string json) =>
        JsonSerializer.Deserialize<SavedTrackList>(json, _options)
        ?? throw new JsonException("The saved Track List is empty.");
}
