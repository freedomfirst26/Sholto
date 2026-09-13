using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sholto.Bench.Scenario;

/// <summary>
/// Parses and validates a scenario file. Validation is deliberately eager and
/// per-action — a scenario with a typo three steps in should fail before step 1
/// runs, not mid-render with a half-written WAV on disk.
/// </summary>
public static class ScenarioParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static Scenario LoadFile(string path) => Parse(File.ReadAllText(path));

    public static Scenario Parse(string json)
    {
        Scenario? scenario;
        try
        {
            scenario = JsonSerializer.Deserialize<Scenario>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"scenario JSON is malformed: {ex.Message}", ex);
        }
        if (scenario is null)
            throw new FormatException("scenario JSON parsed to null");

        for (int i = 0; i < scenario.Actions.Count; i++)
            Validate(scenario.Actions[i], i);

        return scenario;
    }

    private static void Validate(ScenarioAction a, int index)
    {
        void Require(bool ok, string what) =>
            _ = ok ? true : throw new FormatException($"scenario action {index} ('{a.Action}'): {what}");

        switch (a.Action)
        {
            case "load":
                Require(a.Deck is 1 or 2, "requires \"deck\": 1 or 2");
                Require(!string.IsNullOrWhiteSpace(a.Track), "requires \"track\"");
                break;
            case "gain":
                Require(a.Deck is 1 or 2, "requires \"deck\": 1 or 2");
                Require(a.Value is >= 0 and <= 1, "requires \"value\" in 0..1");
                break;
            case "play":
                Require(a.Deck is 1 or 2, "requires \"deck\": 1 or 2");
                break;
            case "crossfader":
                Require(a.Value is >= 0 and <= 1, "requires \"value\" in 0..1");
                break;
            case "wait":
                bool bySeconds = a.Seconds is not null;
                bool byBeats = a.Beats is not null;
                Require(bySeconds != byBeats, "requires exactly one of \"seconds\" or \"beats\"");
                if (byBeats) Require(a.Bpm is > 0, "\"beats\" requires \"bpm\" > 0 (Bench has no real beatgrid to infer it from)");
                break;
            case "scan":
                Require(!string.IsNullOrWhiteSpace(a.Dir), "requires \"dir\" (ui host only)");
                break;
            case "key":
                Require(!string.IsNullOrWhiteSpace(a.Key), "requires \"key\" (ui host only)");
                break;
            case "click":
                Require(!string.IsNullOrWhiteSpace(a.Target), "requires \"target\" (ui host only)");
                break;
            case "screenshot":
                Require(!string.IsNullOrWhiteSpace(a.Out), "requires \"out\" (ui host only)");
                break;
            case "gesture":
                Require(!string.IsNullOrWhiteSpace(a.Event), "requires \"event\" (ui host only)");
                break;
            case "midi":
                Require(a.MidiChannel is > 0, "requires \"midiChannel\" > 0 (ui host only)");
                Require((a.Note is not null) != (a.Cc is not null), "requires exactly one of \"note\" or \"cc\"");
                if (a.Cc is not null) Require(a.CcValue is >= 0 and <= 127, "\"cc\" requires \"ccValue\" in 0..127");
                break;
            default:
                throw new FormatException($"scenario action {index}: unknown action \"{a.Action}\"");
        }
    }
}
