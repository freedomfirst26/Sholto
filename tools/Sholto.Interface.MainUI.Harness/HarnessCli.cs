using System.Text.Json;
using Sholto.Interface.Bench.Scenario;
using Sholto.Interface.MainUI.Harness.State;
using Sholto.Interface.MainUI.Harness.Ui;

namespace Sholto.Interface.MainUI.Harness;

/// <summary>
/// Thin CLI face over the UI-capture code: <c>ui</c> and <c>screenshot</c>, the two verbs that
/// used to live in the bench and need the real window tree. Run as
/// <c>dotnet run --project tools/Sholto.Interface.MainUI.Harness -- ui --scenario x.json</c>
/// and get JSON back.
///
/// <para>Instance, not static: every collaborator is handed in. <c>Program.Main</c>
/// (the one forced static) composes them and runs <c>new HarnessCli(...).Run(args)</c>.</para>
/// </summary>
/// <param name="scenarioParser">Loads and validates scenario files.</param>
/// <param name="uiHost">Mounts the headless UI and runs scenarios against it.</param>
internal sealed class HarnessCli(IScenarioParser scenarioParser, IUiHost uiHost)
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

    private int Usage() { PrintUsage(); return 0; }
    private int Unknown(string cmd) { Console.Error.WriteLine($"unknown subcommand '{cmd}'"); PrintUsage(); return 1; }

    private void PrintUsage()
    {
        Console.Error.WriteLine("""
            Sholto.Interface.MainUI.Harness — drives the real MainUI window tree headlessly (Avalonia.Headless)

            Usage:
              ui         --scenario <json>   (drives the real headless UI tree; prints VM state +
                          what each key/click/gesture/midi step actually changed)
              screenshot [--scenario <json>] --out <png-path>   (drives the UI, if a scenario is
                          given, then captures one frame)

            Offline rendering, measurement, deck state and the window-less scripted controller
            (render, measure, state, headless, latency) are in tools/Sholto.Interface.Bench.

            Scenario JSON: { "actions": [ { "action": "load"|"gain"|"play"|"crossfader"|"wait"|
              "scan"|"key"|"click"|"screenshot"|"gesture"|"midi", ... } ] }.
              "gesture" drives the real controller input/command bus/performance stack with the real
              window as the keyboard — see Scenario.cs and
              Sholto.Interface.Bench/Controller/ScenarioGestureBuilder.cs for the field each
              ControllerEvent needs. "midi" translates a raw NoteEvent/CcEvent through the FLX-4
              mapping first, one layer above "gesture".
            """);
    }

    // ---- ui ----------------------------------------------------------------

    private int Ui(string[] args)
    {
        var opt = ParseFlags(args);
        string scenarioPath = Require(opt, "scenario");
        var scenario = scenarioParser.LoadFile(scenarioPath);

        var (vm, _, driver) = uiHost.RunScenario(scenario);

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            state = new UiStateSnapshot(vm, driver.Decks),
            interactions = driver.Outcomes,
            gestures = driver.GestureOutcomes,
            screenshots = driver.Captures,
        }, _jsonOptions));
        return 0;
    }

    // ---- screenshot --------------------------------------------------------

    private int Screenshot(string[] args)
    {
        var opt = ParseFlags(args);
        string outPath = Require(opt, "out");

        UiScenarioDriver driver;
        if (opt.TryGetValue("scenario", out var scenarioPath))
        {
            var scenario = scenarioParser.LoadFile(scenarioPath);
            (_, _, driver) = uiHost.RunScenario(scenario);
        }
        else
        {
            (_, _, driver) = uiHost.Open();
        }

        var capture = driver.Screenshot(outPath);
        Console.WriteLine(JsonSerializer.Serialize(capture, _jsonOptions));
        return 0;
    }

    // ---- flag parsing ------------------------------------------------------

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
}
