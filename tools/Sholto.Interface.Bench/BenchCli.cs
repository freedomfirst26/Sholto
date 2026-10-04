using System.Text.Json;
using Sholto.App.Audio;
using Sholto.App.Dsp;
using Sholto.Interface.Bench.Rendering;
using Sholto.Interface.Bench.Scenario;
using Sholto.Interface.Bench.Sound;
using Sholto.Interface.Bench.State;
using Sholto.Interface.Bench.Headless;
using Sholto.Interface.Bench.Behaviour;
using SoundFlow.Enums;
using SoundFlow.Structs;

namespace Sholto.Interface.Bench;

/// <summary>
/// Thin CLI face over the render/measure/state/headless/latency library code —
/// subcommands so an agent (or CI) can run
/// <c>dotnet run --project tools/Sholto.Interface.Bench -- render ...</c> and get JSON
/// back. `render`, `measure` and `state` are phase 1a ("ears"/"eyes"); `headless` is the
/// scripted-controller interface (scenario → Commands on the bus). The window-driving verbs
/// `ui` and `screenshot` live in the MainUI harness tool.
///
/// <para>Instance, not static: every collaborator is handed in. <c>Program.Main</c>
/// (the one forced static) composes them and runs <c>new BenchCli(...).Run(args)</c>.</para>
/// </summary>
/// <param name="scenarioParser">Loads and validates scenario files.</param>
/// <param name="offlineRenderer">Renders decks to a WAV.</param>
/// <param name="soundMeter">Measures a WAV through ffmpeg.</param>
/// <param name="benchDeck">Builds the offline engine and decks.</param>
/// <param name="deckAdvance">Advances deck position without rendering.</param>
/// <param name="headlessHost">Builds the headless graph and runs scenarios against it.</param>
/// <param name="crossfade">The crossfade curve the scenario runner applies.</param>
internal sealed class BenchCli(
    IScenarioParser scenarioParser,
    IOfflineRenderer offlineRenderer,
    ISoundMeter soundMeter,
    IBenchDeck benchDeck,
    IDeckAdvance deckAdvance,
    IHeadlessHost headlessHost,
    ICrossfadeCurve crossfade,
    IInputLatencyBenchmark latency)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public int Run(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        try
        {
            return args[0] switch
            {
                "render" => Render(args[1..]),
                "measure" => Measure(args[1..]),
                "state" => StateDump(args[1..]),
                "headless" => Headless(args[1..]),
                "latency" => Latency(),
                "-h" or "--help" => Usage(),
                _ => Unknown(args[0]),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private int Usage() { PrintUsage(); return 0; }
    private int Unknown(string cmd) { Console.Error.WriteLine($"unknown subcommand '{cmd}'"); PrintUsage(); return 1; }

    private void PrintUsage()
    {
        Console.Error.WriteLine("""
            Sholto.Interface.Bench — the headless interface: offline rendering, measurement and scripted controller driving

            Usage:
              render     --track <path> [--track2 <path>] [--gain1 <0..1>] [--gain2 <0..1>]
                          --duration <seconds> --out <wav-path> [--channels 2|4]
                       or --scenario <json> --out <wav-path> [--channels 2|4]
              measure    --wav <path>
              state      --track <path> [--track2 <path>] [--gain1 <0..1>] [--gain2 <0..1>]
                          [--seconds <n>]   (loads + runs the scenario for <n> seconds, then dumps state)
                       or --scenario <json>
              headless   --scenario <json>   (builds the headless core + controller input stack, no window;
                          runs the scenario and prints deck state + what each scan/gesture/midi step
                          actually changed)
              latency    (10,000 jog turns controller -> command -> platter accumulation + per-frame
                          flush, and app event -> controller LED; prints ns/op and bytes/op)

            Window-driving verbs (ui, screenshot) are in the MainUI harness tool.

            Scenario JSON: { "actions": [ { "action": "load"|"gain"|"play"|"crossfader"|"wait"|
              "scan"|"gesture"|"midi", ... } ] }. (key / click / screenshot are harness-only.)
              "gesture" drives the real controller input/command bus/performance stack —
              see Scenario.cs and Sholto.Interface.Bench/Controller/ScenarioGestureBuilder.cs for the
              field each ControllerEvent needs. "midi" translates a raw NoteEvent/CcEvent
              through the FLX-4 mapping first, one layer above "gesture". Both, and "scan",
              are headless-only (they need the core + input stack, which only the headless
              host composes).
            """);
    }

    // ---- render ----------------------------------------------------------

    private int Render(string[] args)
    {
        var opt = ParseFlags(args);
        string outPath = Require(opt, "out");
        int channels = int.Parse(opt.GetValueOrDefault("channels", "2"), System.Globalization.CultureInfo.InvariantCulture);
        var deviceFormat = new AudioFormat { SampleRate = AudioFileDecoder.TargetSampleRate, Channels = channels, Format = SampleFormat.F32 };

        if (opt.TryGetValue("scenario", out var scenarioPath))
        {
            var scenario = scenarioParser.LoadFile(scenarioPath);
            offlineRenderer.RenderScenario(scenario, deviceFormat, outPath);
            Console.WriteLine(JsonSerializer.Serialize(new { wav = outPath, channels, scenario = scenarioPath, actions = scenario.Actions.Count }, _jsonOptions));
            return 0;
        }

        string track1 = Require(opt, "track");
        string track2 = opt.GetValueOrDefault("track2", track1); // default: same track on both decks — the bug scenario
        float gain1 = ParseFloat(opt.GetValueOrDefault("gain1", "1.0"));
        float gain2 = ParseFloat(opt.GetValueOrDefault("gain2", "1.0"));
        double seconds = double.Parse(Require(opt, "duration"), System.Globalization.CultureInfo.InvariantCulture);

        var engine = benchDeck.CreateEngine();
        var deck1 = benchDeck.LoadAndPlay(engine, track1, gain1);
        var deck2 = benchDeck.LoadAndPlay(engine, track2, gain2);

        offlineRenderer.Render([deck1, deck2], engine, deviceFormat, TimeSpan.FromSeconds(seconds), outPath);

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            wav = outPath,
            durationSeconds = seconds,
            channels,
            track1,
            track2,
            gain1,
            gain2,
        }, _jsonOptions));
        return 0;
    }

    // ---- measure -----------------------------------------------------------

    private int Measure(string[] args)
    {
        var opt = ParseFlags(args);
        string wav = Require(opt, "wav");
        var result = soundMeter.Measure(wav);
        Console.WriteLine(JsonSerializer.Serialize(result, _jsonOptions));
        return 0;
    }

    // ---- state ---------------------------------------------------------

    private int StateDump(string[] args)
    {
        var opt = ParseFlags(args);

        if (opt.TryGetValue("scenario", out var scenarioPath))
        {
            var scenario = scenarioParser.LoadFile(scenarioPath);
            var engine = benchDeck.CreateEngine();
            var deck1 = benchDeck.Create(engine);
            var deck2 = benchDeck.Create(engine);
            var decks = new Dictionary<int, Deck> { [1] = deck1, [2] = deck2 };
            var runner = new ScenarioRunner(decks, advance: span => deckAdvance.Advance(decks.Values, span), crossfade);
            runner.Run(scenario);

            var scenarioState = new BenchState
            {
                Decks = [new DeckStateSnapshot(deck1), new DeckStateSnapshot(deck2)],
                CrossfaderPosition = null,
            };
            Console.WriteLine(JsonSerializer.Serialize(scenarioState, _jsonOptions));
            return 0;
        }

        string track1 = Require(opt, "track");
        string track2 = opt.GetValueOrDefault("track2", track1);
        float gain1 = ParseFloat(opt.GetValueOrDefault("gain1", "1.0"));
        float gain2 = ParseFloat(opt.GetValueOrDefault("gain2", "1.0"));
        double seconds = double.Parse(opt.GetValueOrDefault("seconds", "1.0"), System.Globalization.CultureInfo.InvariantCulture);

        var directEngine = benchDeck.CreateEngine();
        var directDeck1 = benchDeck.LoadAndPlay(directEngine, track1, gain1);
        var directDeck2 = benchDeck.LoadAndPlay(directEngine, track2, gain2);

        // Pull enough frames through each deck's own component to advance
        // playback position by `seconds` — same "pull, don't wait" trick
        // DeckAdvance now shares with the scenario runner's "wait" step.
        deckAdvance.Advance([directDeck1, directDeck2], TimeSpan.FromSeconds(seconds));

        var state = new BenchState
        {
            Decks = [new DeckStateSnapshot(directDeck1), new DeckStateSnapshot(directDeck2)],
            CrossfaderPosition = null,
        };
        Console.WriteLine(JsonSerializer.Serialize(state, _jsonOptions));
        return 0;
    }

    // ---- headless ----------------------------------------------------------

    private int Headless(string[] args)
    {
        var opt = ParseFlags(args);
        string scenarioPath = Require(opt, "scenario");
        var scenario = scenarioParser.LoadFile(scenarioPath);

        var session = headlessHost.RunScenario(scenario);

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            decks = new[] { new DeckStateSnapshot(session.Deck1), new DeckStateSnapshot(session.Deck2) },
            interactions = session.Driver.Outcomes,
            gestures = session.Driver.GestureOutcomes,
        }, _jsonOptions));
        return 0;
    }

    private int Latency()
    {
        Console.WriteLine(JsonSerializer.Serialize(latency.Run(), _jsonOptions));
        return 0;
    }

    // ---- flag parsing ----------------------------------------------------

    private Dictionary<string, string> ParseFlags(string[] args)
    {
        var result = new Dictionary<string, string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) continue;
            string key = args[i][2..];
            string value = i + 1 < args.Length ? args[++i] : "";
            result[key] = value;
        }
        return result;
    }

    private string Require(Dictionary<string, string> opt, string key) =>
        opt.TryGetValue(key, out var v) ? v : throw new ArgumentException($"missing required --{key}");

    private float ParseFloat(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
}
