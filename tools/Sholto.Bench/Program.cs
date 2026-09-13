using System.Text.Json;
using Sholto.Audio;
using Sholto.Bench.Rendering;
using Sholto.Bench.Scenario;
using Sholto.Bench.Sound;
using Sholto.Bench.State;
using Sholto.Bench.Ui;
using SoundFlow.Enums;
using SoundFlow.Structs;

namespace Sholto.Bench;

/// <summary>
/// Thin CLI face over the render/measure/state/ui/screenshot library code —
/// subcommands so an agent (or CI) can run
/// <c>dotnet run --project tools/Sholto.Bench -- render ...</c> and get JSON
/// back. `render`, `measure` and `state` are phase 1a ("ears"/"eyes"); `ui` and
/// `screenshot` are phase 1b ("hands") — see ~/Projects/sholto.md, "Design —
/// Sholto.Bench".
/// </summary>
internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static int Main(string[] args)
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
                "ui" => Ui(args[1..]),
                "screenshot" => Screenshot(args[1..]),
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

    private static int Usage() { PrintUsage(); return 0; }
    private static int Unknown(string cmd) { Console.Error.WriteLine($"unknown subcommand '{cmd}'"); PrintUsage(); return 1; }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("""
            Sholto.Bench — offline rendering, measurement and headless UI driving

            Usage:
              render     --track <path> [--track2 <path>] [--gain1 <0..1>] [--gain2 <0..1>]
                          --duration <seconds> --out <wav-path> [--channels 2|4]
                       or --scenario <json> --out <wav-path> [--channels 2|4]
              measure    --wav <path>
              state      --track <path> [--track2 <path>] [--gain1 <0..1>] [--gain2 <0..1>]
                          [--seconds <n>]   (loads + runs the scenario for <n> seconds, then dumps state)
                       or --scenario <json>
              ui         --scenario <json>   (drives the real headless UI tree; prints VM state +
                          what each key/click actually changed)
              screenshot [--scenario <json>] --out <png-path>   (drives the UI, if a scenario is
                          given, then captures one frame)

            Scenario JSON: { "actions": [ { "action": "load"|"gain"|"play"|"crossfader"|"wait"|
              "scan"|"key"|"click"|"screenshot"|"gesture"|"midi", ... } ] }.
              "gesture" drives the real GestureRecognizer/GestureBus/Orchestrator stack —
              see Scenario.cs and Sholto.Bench/Controller/ScenarioGestureBuilder.cs for the
              field each ControllerEvent needs. "midi" translates a raw NoteEvent/CcEvent
              through the FLX-4 mapping first, one layer above "gesture". Both are ui-only
              (need the real MainViewModel + Orchestrator, which only the ui/screenshot
              hosts compose). See ~/Projects/sholto.md.
            """);
    }

    // ---- render ----------------------------------------------------------

    private static int Render(string[] args)
    {
        var opt = ParseFlags(args);
        string outPath = Require(opt, "out");
        int channels = int.Parse(opt.GetValueOrDefault("channels", "2"), System.Globalization.CultureInfo.InvariantCulture);
        var deviceFormat = new AudioFormat { SampleRate = AudioFileDecoder.TargetSampleRate, Channels = channels, Format = SampleFormat.F32 };

        if (opt.TryGetValue("scenario", out var scenarioPath))
        {
            var scenario = ScenarioParser.LoadFile(scenarioPath);
            OfflineRenderer.RenderScenario(scenario, deviceFormat, outPath);
            Console.WriteLine(JsonSerializer.Serialize(new { wav = outPath, channels, scenario = scenarioPath, actions = scenario.Actions.Count }, JsonOptions));
            return 0;
        }

        string track1 = Require(opt, "track");
        string track2 = opt.GetValueOrDefault("track2", track1); // default: same track on both decks — the bug scenario
        float gain1 = ParseFloat(opt.GetValueOrDefault("gain1", "1.0"));
        float gain2 = ParseFloat(opt.GetValueOrDefault("gain2", "1.0"));
        double seconds = double.Parse(Require(opt, "duration"), System.Globalization.CultureInfo.InvariantCulture);

        var engine = BenchDeck.CreateEngine();
        var deck1 = BenchDeck.LoadAndPlay(engine, track1, gain1);
        var deck2 = BenchDeck.LoadAndPlay(engine, track2, gain2);

        OfflineRenderer.Render([deck1, deck2], engine, deviceFormat, TimeSpan.FromSeconds(seconds), outPath);

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            wav = outPath,
            durationSeconds = seconds,
            channels,
            track1,
            track2,
            gain1,
            gain2,
        }, JsonOptions));
        return 0;
    }

    // ---- measure -----------------------------------------------------------

    private static int Measure(string[] args)
    {
        var opt = ParseFlags(args);
        string wav = Require(opt, "wav");
        var result = FfmpegMeasurer.Measure(wav);
        Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        return 0;
    }

    // ---- state ---------------------------------------------------------

    private static int StateDump(string[] args)
    {
        var opt = ParseFlags(args);

        if (opt.TryGetValue("scenario", out var scenarioPath))
        {
            var scenario = ScenarioParser.LoadFile(scenarioPath);
            var engine = BenchDeck.CreateEngine();
            var deck1 = BenchDeck.Create(engine);
            var deck2 = BenchDeck.Create(engine);
            var decks = new Dictionary<int, Deck> { [1] = deck1, [2] = deck2 };
            var runner = new ScenarioRunner(decks, advance: span => DeckAdvance.Advance(decks.Values, span));
            runner.Run(scenario);

            var scenarioState = new BenchState
            {
                Decks = [DeckStateSnapshot.From(deck1), DeckStateSnapshot.From(deck2)],
                CrossfaderPosition = null,
            };
            Console.WriteLine(JsonSerializer.Serialize(scenarioState, JsonOptions));
            return 0;
        }

        string track1 = Require(opt, "track");
        string track2 = opt.GetValueOrDefault("track2", track1);
        float gain1 = ParseFloat(opt.GetValueOrDefault("gain1", "1.0"));
        float gain2 = ParseFloat(opt.GetValueOrDefault("gain2", "1.0"));
        double seconds = double.Parse(opt.GetValueOrDefault("seconds", "1.0"), System.Globalization.CultureInfo.InvariantCulture);

        var directEngine = BenchDeck.CreateEngine();
        var directDeck1 = BenchDeck.LoadAndPlay(directEngine, track1, gain1);
        var directDeck2 = BenchDeck.LoadAndPlay(directEngine, track2, gain2);

        // Pull enough frames through each deck's own component to advance
        // playback position by `seconds` — same "pull, don't wait" trick
        // DeckAdvance now shares with the scenario runner's "wait" step.
        DeckAdvance.Advance([directDeck1, directDeck2], TimeSpan.FromSeconds(seconds));

        var state = new BenchState
        {
            Decks = [DeckStateSnapshot.From(directDeck1), DeckStateSnapshot.From(directDeck2)],
            CrossfaderPosition = null,
        };
        Console.WriteLine(JsonSerializer.Serialize(state, JsonOptions));
        return 0;
    }

    // ---- ui ----------------------------------------------------------------

    private static int Ui(string[] args)
    {
        var opt = ParseFlags(args);
        string scenarioPath = Require(opt, "scenario");
        var scenario = ScenarioParser.LoadFile(scenarioPath);

        var (vm, _, driver) = UiHost.RunScenario(scenario);

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            state = UiStateSnapshot.From(vm),
            interactions = driver.Outcomes,
            gestures = driver.GestureOutcomes,
            screenshots = driver.Captures,
        }, JsonOptions));
        return 0;
    }

    // ---- screenshot ----------------------------------------------------------------

    private static int Screenshot(string[] args)
    {
        var opt = ParseFlags(args);
        string outPath = Require(opt, "out");

        Sholto.App.ViewModels.MainViewModel vm;
        UiScenarioDriver driver;
        if (opt.TryGetValue("scenario", out var scenarioPath))
        {
            var scenario = ScenarioParser.LoadFile(scenarioPath);
            (vm, _, driver) = UiHost.RunScenario(scenario);
        }
        else
        {
            var (composedVm, window) = BenchAppComposer.Create();
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            vm = composedVm;
            driver = new UiScenarioDriver(window, composedVm, new Sholto.Bench.Controller.GestureHost(composedVm, window));
        }

        var capture = driver.Screenshot(outPath);
        Console.WriteLine(JsonSerializer.Serialize(capture, JsonOptions));
        return 0;
    }

    // ---- flag parsing ----------------------------------------------------

    private static Dictionary<string, string> ParseFlags(string[] args)
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

    private static string Require(Dictionary<string, string> opt, string key) =>
        opt.TryGetValue(key, out var v) ? v : throw new ArgumentException($"missing required --{key}");

    private static float ParseFloat(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
}
