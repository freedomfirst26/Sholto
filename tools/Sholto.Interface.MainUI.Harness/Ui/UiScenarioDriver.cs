using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Sholto.App;
using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels;
using Sholto.Interface.MainUI.Views;
using Sholto.App.Audio;
using Sholto.App.ExternalTools;
using Sholto.Interface.Bench.Behaviour;
using Sholto.Interface.Bench.Controller;
using Sholto.Interface.Bench.Headless;
using Sholto.Interface.Bench.Scenario;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Stems;
using MusicalKey = Sholto.App.Analysis.Harmony.Key;
using IKeyFactory = Sholto.App.Analysis.Harmony.IKeyFactory;
using Sholto.Interface.MainUI.Harness.Appearance;
using Sholto.Interface.MainUI.Harness.Showcase;

namespace Sholto.Interface.MainUI.Harness.Ui;

/// <summary>
/// Handles the ui-only scenario actions (key, type, wait, click, screenshot) that
/// <see cref="ScenarioRunner"/> hands off via
/// <c>OnUiAction</c> — real <c>Avalonia.Headless</c> input against the real
/// <see cref="MainWindow"/>, so a scenario's "press 1" or "click the first
/// track" is exactly the same gesture a person makes.
///
/// key and click are tracked: a small set of observable VM/deck fields is
/// snapshotted before and after, and only the fields that actually changed are
/// recorded as an <see cref="InteractionOutcome"/> in <see cref="Outcomes"/> —
/// the record of "did this do anything", not just "did it throw".
/// </summary>
/// <param name="window">The real main window the input is sent to.</param>
/// <param name="vm">The main view model whose observable fields are snapshotted.</param>
/// <param name="gestureHost">Composes the real gesture recognizer / command bus /
/// Orchestrator stack that "gesture" and "midi" steps drive — see
/// <see cref="GestureHost"/>.</param>
/// <param name="core">The headless core: the decks' ports and the library, read and driven directly
/// because the view model projects the App's events and no longer exposes them.</param>
/// <param name="decks">The concrete Deck1 and Deck2 behind the view model (index 0 and 1),
/// for the internal members the view model's ports do not expose.</param>
/// <param name="gestureFactory">Turns a "gesture" step into the <c>ControllerEvent</c> it names.</param>
/// <param name="snapshot">Reads the observable core/deck fields a step is diffed on.</param>
/// <param name="diff">Finds which of them changed.</param>
/// <param name="showcase">Makes up the analysis an "analyse" step gives a loaded deck.</param>
/// <param name="keys">Parses the Camelot codes of "analyse" and "row" steps.</param>
/// <param name="publisher">The bus a "controller" step announces the controller's connection on.</param>
/// <param name="uiClock">The clock the view models' per-frame work runs on, ticked while a step settles.</param>
/// <param name="demoLibrary">The real library database the "scan" and "database" steps use, filled with made-up crates and tags.</param>
public sealed class UiScenarioDriver(MainWindow window, MainViewModel vm, GestureHost gestureHost, CoreStack core, IReadOnlyList<Deck> decks,
    IScenarioGestureFactory gestureFactory, ICoreSnapshot snapshot, IStateDiff diff, IShowcaseAnalysisFactory showcase,
    IKeyFactory keys, IEventPublisher publisher, ManualFrameClock uiClock, IDemoLibrary demoLibrary)
{
    /// <summary>How long a screenshot lets background work (preview bakes, posted analysis events) land
    /// before it captures, ticking the performance clock so the decks' play positions are current.</summary>
    private const int SettleMs = 400;

    /// <summary>How long an "analyse" step waits for its deck's track to finish loading.</summary>
    private const int LoadTimeoutMs = 15000;

    private readonly MainWindow _window = window;
    private readonly MainViewModel _vm = vm;
    private readonly CoreStack _core = core;
    private readonly GestureHost _gestureHost = gestureHost;
    private readonly IScenarioGestureFactory _gestureFactory = gestureFactory;
    private readonly ICoreSnapshot _snapshot = snapshot;
    private readonly IStateDiff _diff = diff;
    private readonly IShowcaseAnalysisFactory _showcase = showcase;
    private readonly IKeyFactory _keys = keys;
    private readonly IEventPublisher _publisher = publisher;
    private readonly ManualFrameClock _uiClock = uiClock;
    private readonly IDemoLibrary _demoLibrary = demoLibrary;

    /// <summary>The concrete Deck1 and Deck2 (index 0 and 1) this driver snapshots.</summary>
    public IReadOnlyList<Deck> Decks { get; } = decks;

    public List<InteractionOutcome> Outcomes { get; } = [];
    public List<GestureOutcome> GestureOutcomes { get; } = [];
    public List<AppearanceCapture> Captures { get; } = [];

    public void Handle(ScenarioAction a)
    {
        switch (a.Action)
        {
            case "scan":
                Outcomes.Add(Track("scan", $"scan {a.Dir}", () => Scan(a.Dir!)));
                break;
            case "key":
                Outcomes.Add(Track("key", $"key {a.Key}", () => PressKey(a.Key!, a.Shifted ?? false, a.Ctrl ?? false)));
                break;
            case "type":
                Outcomes.Add(Track("type", $"type \"{a.Text}\"", () => TypeText(a.Text!)));
                break;
            case "wait":
                // The runner has already advanced the decks; this lets posted work land and ticks the clock.
                Settle((int)(a.Seconds!.Value * 1000));
                break;
            case "click":
                int index = a.Index ?? 0;
                Outcomes.Add(Track("click", $"click {a.Target}[{index}]", () => ClickTarget(a.Target!, index)));
                break;
            case "screenshot":
                Captures.Add(Screenshot(a.Out!, a.Scale ?? 1, a.Seconds is { } settle ? (int)(settle * 1000) : SettleMs));
                break;
            case "analyse":
                Outcomes.Add(Track("analyse", $"analyse deck {a.Deck}", () => Analyse(a.Deck!.Value, a.Seconds!.Value, a.Camelot)));
                break;
            case "controller":
                Outcomes.Add(Track("controller", $"controller {(a.On!.Value ? "plugged in" : "unplugged")}", () =>
                {
                    _publisher.Publish(new DeviceConnectionChanged(a.On!.Value));
                    Dispatcher.UIThread.RunJobs();
                }));
                break;
            case "database":
                // The library database is "up": the demo crates and tags are filed through the real services, which
                // are attached to the library (it announces the database), so Glance asks them for its rail and chips.
                Outcomes.Add(Track("database", "library database attached", () =>
                {
                    RunPumping(_demoLibrary.SeedAsync(_core.Library));
                    Settle();
                }));
                break;
            case "row":
                Outcomes.Add(Track("row", $"row {Path.GetFileName(a.Track)}", () => Row(a.Track!, a.Bpm!.Value, a.Camelot)));
                break;
            case "gesture":
                GestureOutcomes.Add(Gesture(a));
                break;
            case "midi":
                GestureOutcomes.Add(Midi(a));
                break;
            default:
                throw new NotSupportedException($"UiScenarioDriver doesn't handle \"{a.Action}\"");
        }
    }

    /// <summary>Build the named ControllerEvent (<see cref="ScenarioGestureFactory"/>)
    /// and send it through the real pipeline (<see cref="GestureHost.SendGesture"/>),
    /// diffing deck/VM state around it exactly like <see cref="Track"/> does for
    /// key/click — an outcome with an empty Changed means the gesture ran but
    /// visibly moved nothing, which is the finding a harness must not hide.</summary>
    private GestureOutcome Gesture(ScenarioAction a)
    {
        var evt = _gestureFactory.Create(a);
        var before = Snapshot();
        _gestureHost.SendGesture(evt);
        var after = Snapshot();
        return new GestureOutcome
        {
            Action = "gesture",
            Description = $"gesture {a.Event} deck={a.Deck}",
            ControllerEvent = evt.ToString(),
            Changed = _diff.Between(before, after),
        };
    }

    /// <summary>Translate the raw note/CC through the FLX-4 mapping
    /// (<see cref="GestureHost.SendMidiNote"/>/<see cref="GestureHost.SendMidiCc"/>)
    /// — one layer above <see cref="Gesture"/> — and, if it resolved, send the
    /// result through the same pipeline. <see cref="GestureOutcome.ControllerEvent"/>
    /// is null when the wire number is unmapped, same as it would be on real
    /// hardware.</summary>
    private GestureOutcome Midi(ScenarioAction a)
    {
        var before = Snapshot();
        Sholto.Interface.Controller.ControllerEvent? evt = a.Note is { } note
            ? _gestureHost.SendMidiNote(a.MidiChannel!.Value, note, a.Velocity ?? (a.IsDown ?? true ? 127 : 0), a.IsDown ?? true)
            : _gestureHost.SendMidiCc(a.MidiChannel!.Value, a.Cc!.Value, a.CcValue!.Value);
        var after = Snapshot();
        string what = a.Note is { } n ? $"note 0x{n:X2}" : $"cc 0x{a.Cc:X2}={a.CcValue}";
        return new GestureOutcome
        {
            Action = "midi",
            Description = $"midi ch={a.MidiChannel} {what}",
            ControllerEvent = evt?.ToString(),
            Changed = _diff.Between(before, after),
        };
    }

    /// <summary>Real filesystem scan through the same <c>ITrackScanner</c> the
    /// app uses (<see cref="BenchAppFactory.Create"/> wires a real
    /// <c>TrackScanner</c>, not a fake) — no DB factory, so scanned tracks never
    /// persist, matching every other Bench run being throwaway.
    /// <c>LibrarySession.ScanAsync</c> posts its final "apply the scan to the rows"
    /// step through the app thread (Avalonia's <c>Dispatcher.UIThread</c> here) — a plain
    /// <c>.GetAwaiter().GetResult()</c> on this (the UI) thread would block
    /// waiting for a dispatcher job that can only ever run on this same,
    /// now-blocked thread. So this pumps the dispatcher queue while it waits
    /// instead of just blocking on it.</summary>
    /// <summary>Scans over the demo database, so each track has the id the crates and tags are filed under.</summary>
    private void Scan(string dir) => RunPumping(ScanAsync(dir));

    private async Task ScanAsync(string dir) => await _core.Library.ScanAsync(dir, await _demoLibrary.OpenAsync());

    private void RunPumping(Task task)
    {
        while (!task.IsCompleted)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(1);
        }
        task.GetAwaiter().GetResult(); // rethrow, if it faulted
    }

    /// <summary>Gives the track loaded on <paramref name="deck"/> a made-up analysis (waveform, grid,
    /// BPM, stems, and the key if one is given), as if the analysers had finished: the harness has none, so
    /// without this a loaded deck shows no waveform. Goes through the deck's real
    /// <c>TrackAnalysis</c>, so every consumer reacts exactly as it does to real results.</summary>
    private void Analyse(int deck, double seconds, string? camelot)
    {
        // A key-press load decodes off the UI thread; the deck swaps in a fresh TrackAnalysis when the
        // samples land, so wait for that before filling it.
        var until = DateTime.UtcNow.AddMilliseconds(LoadTimeoutMs);
        while (!Decks[deck - 1].IsLoaded && DateTime.UtcNow < until)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
        if (!Decks[deck - 1].IsLoaded)
            throw new InvalidOperationException($"analyse: nothing loaded on deck {deck} after {LoadTimeoutMs} ms");
        var analysis = Decks[deck - 1].Analysis;
        analysis.Set(_showcase.Create(seconds));
        if (_keys.TryFromCamelot(camelot, out var key)) analysis.Set(new KeyAnalysis(key));
        // Stems "landed" so the stem chips show; nothing reads the files unless a stem is played, and the
        // harness plays no audio.
        analysis.Set(new StemPaths(Path.Combine(Path.GetTempPath(), "sholto-harness-showcase-stems")));
        Settle();
    }

    /// <summary>Shows a BPM (and a key, if given) on a library row, the way a finished analysis does.</summary>
    private void Row(string track, double bpm, string? camelot)
    {
        MusicalKey? key = _keys.TryFromCamelot(camelot, out var k) ? k : null;
        _core.Library.ApplyReanalysis(Path.GetFullPath(track), bpm, key);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Lets posted work land: pumps the dispatcher and ticks the performance clock for
    /// <paramref name="ms"/> milliseconds.</summary>
    private void Settle(int ms = SettleMs)
    {
        var until = DateTime.UtcNow.AddMilliseconds(ms);
        while (DateTime.UtcNow < until)
        {
            _gestureHost.Clock.Tick();
            _uiClock.Tick();
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
    }

    private InteractionOutcome Track(string action, string description, Action act)
    {
        var before = Snapshot();
        act();
        var after = Snapshot();
        return new InteractionOutcome { Action = action, Description = description, Changed = _diff.Between(before, after) };
    }

    /// <summary>The core's and decks' fields, plus what the view model shows.</summary>
    private Dictionary<string, object?> Snapshot()
    {
        var fields = _snapshot.Take(_core, Decks);
        fields["SelectedTrackIndex"] = _vm.SelectedTrackIndex;
        fields["TracksCount"] = _vm.Tracks.Count;
        fields["IsSearchOpen"] = _vm.IsSearchOpen;
        fields["Glance.Query"] = _vm.Glance.Query;
        fields["Glance.Chips"] = _vm.Glance.Chips.Count;
        fields["Glance.Rows"] = _vm.Glance.Rows.Count;
        fields["Glance.Target"] = _vm.Glance.Target;
        fields["Crossfader.Vm"] = _vm.Crossfader;
        return fields;
    }

    /// <summary>Presses and releases a named <see cref="Key"/> (e.g. "D1", "Space",
    /// "M", "Escape") with Shift and/or Ctrl held if asked. <see cref="MainWindow.OnGlobalKeyDown"/>
    /// only reads <c>e.Key</c> and <c>e.KeyModifiers</c> — never the physical-key/
    /// text-symbol pair — so <see cref="PhysicalKey.None"/> and a null symbol are
    /// honest here, not a workaround.</summary>
    private void PressKey(string keyName, bool shifted, bool ctrl)
    {
        var key = Enum.Parse<Key>(keyName, ignoreCase: true);
        var mods = (shifted ? RawInputModifiers.Shift : RawInputModifiers.None) | (ctrl ? RawInputModifiers.Control : RawInputModifiers.None);
        _window.KeyPress(key, mods, PhysicalKey.None, null);
        _window.KeyRelease(key, mods, PhysicalKey.None, null);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Sends <paramref name="text"/> to the focused element as text input.</summary>
    private void TypeText(string text)
    {
        _window.KeyTextInput(text);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Clicks the centre of row <paramref name="index"/> of the named
    /// control. "TrackList" (the library <see cref="ListBox"/>, named in
    /// MainWindow.axaml) is a real click; "LayoutWizard" / "Settings" do what Settings ▸ Layout Wizard… / Settings… do
    /// (headless menus do not open as popups). The vocabulary stays honest about what this
    /// harness can actually reach today rather than pretending to be general.</summary>
    private void ClickTarget(string target, int index)
    {
        if (target == "LayoutWizard")
        {
            _vm.OpenLayoutWizard();
            Dispatcher.UIThread.RunJobs();
            return;
        }
        if (target == "Settings")
        {
            _vm.OpenSettings();
            Dispatcher.UIThread.RunJobs();
            return;
        }
        if (target == "SystemReport")
        {
            // A degraded boot-time probe (demucs and ffmpeg missing), then the amber dot's own entry point.
            _publisher.Publish(new SystemCheck([
                new ToolPresence(ExternalToolNames.Madmom, "beats", true, "/usr/bin/madmom"),
                new ToolPresence(ExternalToolNames.Demucs, "stems", false, null),
                new ToolPresence(ExternalToolNames.Ffmpeg, "transcode", false, null)]).ToReported());
            _vm.OpenSystemReport();
            Dispatcher.UIThread.RunJobs();
            return;
        }
        if (target == "CratePicker")
        {
            // Settings ▸ the library's Add to crate: needs a scanned library (the first row is the track), the
            // database-attached event that builds the picker, and a few made-up crates (no database behind them).
            _publisher.Publish(new LibraryDatabaseAttached(true));
            var picker = _vm.CratePicker ?? throw new InvalidOperationException("CratePicker was not built.");
            var row = _vm.Tracks.FirstOrDefault() ?? throw new InvalidOperationException("CratePicker needs a scanned library first.");
            _vm.OpenCratePickerAsync(row).GetAwaiter().GetResult();
            Settle();
            picker.Options.Clear();
            foreach (var crate in new[] { ("All Tracks", 40), ("Warmup", 5), ("Peak Time", 9), ("Closing", 7) })
                picker.Options.Add(new CratePickerOption(false, crate.Item1, 1, crate.Item2));
            picker.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            return;
        }
        if (target == "FaceplateButton")
        {
            Dispatcher.UIThread.RunJobs();
            var button = _window.FindControl<Button>("FaceplateButton")
                ?? throw new InvalidOperationException("FaceplateButton not found in MainWindow's visual tree.");
            var at = button.TranslatePoint(new Avalonia.Point(button.Bounds.Width / 2, button.Bounds.Height / 2), _window)
                ?? throw new InvalidOperationException("FaceplateButton is not in the window's visual tree.");
            _window.MouseDown(at, MouseButton.Left);
            _window.MouseUp(at, MouseButton.Left);
            // A person's pointer does not stay on the button: park it mid-window, or the icon counts as "found" the
            // moment the guide shrinks back under it and the hint ends at once.
            _window.MouseMove(new Avalonia.Point(_window.Bounds.Width / 2, _window.Bounds.Height / 2));
            Dispatcher.UIThread.RunJobs();
            return;
        }
        if (target != "TrackList")
            throw new NotSupportedException(
                $"click target \"{target}\" isn't wired — only \"TrackList\", \"FaceplateButton\", \"LayoutWizard\", \"Settings\" and \"SystemReport\" are.");

        Dispatcher.UIThread.RunJobs();
        var listBox = _window.FindControl<ListBox>("TrackList")
            ?? throw new InvalidOperationException("TrackList control not found in MainWindow's visual tree.");
        if (listBox.ContainerFromIndex(index) is not Control container)
            throw new InvalidOperationException($"TrackList has no row at index {index} (does the scenario load a library first?).");

        var center = new Avalonia.Point(container.Bounds.Width / 2, container.Bounds.Height / 2);
        var pointInWindow = container.TranslatePoint(center, _window) ?? center;

        _window.MouseDown(pointInWindow, MouseButton.Left);
        _window.MouseUp(pointInWindow, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Lets background work settle, renders one frame and writes it as a PNG. Requires the
    /// headless app to have been started with <c>UseHeadlessDrawing = false</c> (see
    /// <see cref="BenchHeadlessApp"/>) — otherwise <c>CaptureRenderedFrame</c> returns null. At a
    /// <paramref name="scale"/> other than 1 the window is first switched to that render scaling (see
    /// <see cref="SetRenderScaling"/>), so the real compositor lays out and draws at that many pixels
    /// per window pixel — text, waveforms and all — exactly as on a HiDPI screen.</summary>
    public AppearanceCapture Screenshot(string outPath, double scale = 1, int settleMs = SettleMs)
    {
        if (scale != 1) SetRenderScaling(scale);
        try
        {
            Settle(settleMs);
            using var frame = _window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException(
                    "CaptureRenderedFrame returned null — headless drawing must be disabled (UseHeadlessDrawing = false) to get real pixels.");

            string fullPath = Path.GetFullPath(outPath);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using (var fs = File.Create(fullPath))
                frame.Save(fs);

            return new AppearanceCapture
            {
                Path = fullPath,
                Width = frame.PixelSize.Width,
                Height = frame.PixelSize.Height,
                FileSizeBytes = new FileInfo(fullPath).Length,
                IsBlank = IsBlank(frame),
            };
        }
        finally
        {
            if (scale != 1) SetRenderScaling(1);
        }
    }

    /// <summary>Avalonia.Headless fixes a window's render scaling at 1 and has no option for it, so this
    /// sets the headless window's <c>RenderScaling</c> and raises its <c>ScalingChanged</c> the way a
    /// real platform window does when it moves to a HiDPI screen. Reflection over Avalonia.Headless
    /// internals (11.3.x): a dev-tool workaround that throws, rather than silently capturing at 1x,
    /// if a future Avalonia renames them. (<c>RenderTargetBitmap</c> was tried first: it misplaces the
    /// text inside the library's key chips.)</summary>
    private void SetRenderScaling(double scale)
    {
        var impl = _window.PlatformImpl
            ?? throw new InvalidOperationException("the window has no platform implementation");
        var type = impl.GetType();
        var field = type.GetField("<RenderScaling>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new NotSupportedException($"{type.Name} has no RenderScaling backing field; cannot capture at scale {scale}");
        field.SetValue(impl, scale);
        var changed = type.GetProperty("ScalingChanged")?.GetValue(impl) as Action<double>;
        changed?.Invoke(scale);
        Dispatcher.UIThread.RunJobs();
    }

    private unsafe bool IsBlank(Avalonia.Media.Imaging.Bitmap frame)
    {
        int stride = frame.PixelSize.Width * 4;
        int byteCount = stride * frame.PixelSize.Height;
        if (byteCount == 0) return true;
        var buffer = new byte[byteCount];
        fixed (byte* p = buffer)
            frame.CopyPixels(new PixelRect(frame.PixelSize), (IntPtr)p, byteCount, stride);
        byte first = buffer[0];
        for (int i = 1; i < buffer.Length; i++)
            if (buffer[i] != first) return false;
        return true;
    }
}
