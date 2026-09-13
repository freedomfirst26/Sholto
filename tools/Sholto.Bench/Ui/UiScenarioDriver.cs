using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Sholto.App.ViewModels;
using Sholto.App.Views;
using Sholto.Bench.Appearance;
using Sholto.Bench.Behaviour;
using Sholto.Bench.Controller;
using Sholto.Bench.Scenario;

namespace Sholto.Bench.Ui;

/// <summary>
/// Handles the ui-only scenario actions (key, click, screenshot) that
/// <see cref="Sholto.Bench.Scenario.ScenarioRunner"/> hands off via
/// <c>OnUiAction</c> — real <c>Avalonia.Headless</c> input against the real
/// <see cref="MainWindow"/>, so a scenario's "press 1" or "click the first
/// track" is exactly the same gesture a person makes.
///
/// key and click are tracked: a small set of observable VM/deck fields is
/// snapshotted before and after, and only the fields that actually changed are
/// recorded as an <see cref="InteractionOutcome"/> in <see cref="Outcomes"/> —
/// the record of "did this do anything", not just "did it throw".
/// </summary>
public sealed class UiScenarioDriver
{
    private readonly MainWindow _window;
    private readonly MainViewModel _vm;
    private readonly GestureHost _gestureHost;

    public List<InteractionOutcome> Outcomes { get; } = [];
    public List<GestureOutcome> GestureOutcomes { get; } = [];
    public List<AppearanceCapture> Captures { get; } = [];

    /// <param name="gestureHost">Composes the real GestureRecognizer/GestureBus/
    /// Orchestrator stack that "gesture" and "midi" steps drive — see
    /// <see cref="Sholto.Bench.Controller.GestureHost"/>.</param>
    public UiScenarioDriver(MainWindow window, MainViewModel vm, GestureHost gestureHost)
    {
        _window = window;
        _vm = vm;
        _gestureHost = gestureHost;
    }

    public void Handle(ScenarioAction a)
    {
        switch (a.Action)
        {
            case "scan":
                Outcomes.Add(Track("scan", $"scan {a.Dir}", () => Scan(a.Dir!)));
                break;
            case "key":
                Outcomes.Add(Track("key", $"key {a.Key}", () => PressKey(a.Key!)));
                break;
            case "click":
                int index = a.Index ?? 0;
                Outcomes.Add(Track("click", $"click {a.Target}[{index}]", () => ClickTarget(a.Target!, index)));
                break;
            case "screenshot":
                Captures.Add(Screenshot(a.Out!));
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

    /// <summary>Build the named ControllerEvent (<see cref="ScenarioGestureBuilder"/>)
    /// and send it through the real pipeline (<see cref="GestureHost.SendGesture"/>),
    /// diffing deck/VM state around it exactly like <see cref="Track"/> does for
    /// key/click — an outcome with an empty Changed means the gesture ran but
    /// visibly moved nothing, which is the finding a harness must not hide.</summary>
    private GestureOutcome Gesture(ScenarioAction a)
    {
        var evt = ScenarioGestureBuilder.Build(a);
        var before = Snapshot();
        _gestureHost.SendGesture(evt);
        var after = Snapshot();
        return new GestureOutcome
        {
            Action = "gesture",
            Description = $"gesture {a.Event} deck={a.Deck}",
            ControllerEvent = evt.ToString(),
            Changed = Diff(before, after),
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
        Sholto.Controller.ControllerEvent? evt = a.Note is { } note
            ? _gestureHost.SendMidiNote(a.MidiChannel!.Value, note, a.Velocity ?? (a.IsDown ?? true ? 127 : 0), a.IsDown ?? true)
            : _gestureHost.SendMidiCc(a.MidiChannel!.Value, a.Cc!.Value, a.CcValue!.Value);
        var after = Snapshot();
        string what = a.Note is { } n ? $"note 0x{n:X2}" : $"cc 0x{a.Cc:X2}={a.CcValue}";
        return new GestureOutcome
        {
            Action = "midi",
            Description = $"midi ch={a.MidiChannel} {what}",
            ControllerEvent = evt?.ToString(),
            Changed = Diff(before, after),
        };
    }

    private static Dictionary<string, FieldChange> Diff(
        Dictionary<string, object?> before, Dictionary<string, object?> after)
    {
        var changed = new Dictionary<string, FieldChange>();
        foreach (var (key, beforeValue) in before)
        {
            var afterValue = after[key];
            if (!Equals(beforeValue, afterValue))
                changed[key] = new FieldChange { Before = beforeValue, After = afterValue };
        }
        return changed;
    }

    /// <summary>Real filesystem scan through the same <c>ITrackScanner</c> the
    /// app uses (<see cref="BenchAppComposer.Create"/> wires a real
    /// <c>TrackScanner</c>, not a fake) — no DB factory, so scanned tracks never
    /// persist, matching every other Bench run being throwaway.
    /// <c>MusicLibrary.ScanAsync</c> posts its final "apply the scan to Tracks"
    /// step through <c>Dispatcher.UIThread.InvokeAsync</c> — a plain
    /// <c>.GetAwaiter().GetResult()</c> on this (the UI) thread would block
    /// waiting for a dispatcher job that can only ever run on this same,
    /// now-blocked thread. So this pumps the dispatcher queue while it waits
    /// instead of just blocking on it.</summary>
    private void Scan(string dir) => RunPumping(_vm.Library.ScanAsync(dir, null));

    private static void RunPumping(Task task)
    {
        while (!task.IsCompleted)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(1);
        }
        task.GetAwaiter().GetResult(); // rethrow, if it faulted
    }

    private InteractionOutcome Track(string action, string description, Action act)
    {
        var before = Snapshot();
        act();
        var after = Snapshot();
        return new InteractionOutcome { Action = action, Description = description, Changed = Diff(before, after) };
    }

    private Dictionary<string, object?> Snapshot() => new()
    {
        ["SelectedTrackIndex"] = _vm.SelectedTrackIndex,
        ["TracksCount"] = _vm.Tracks.Count,
        ["IsSearchOpen"] = _vm.IsSearchOpen,
        ["Crossfader"] = _vm.Crossfader,
        ["Deck1.IsLoaded"] = _vm.Deck1.Player.IsLoaded,
        ["Deck1.IsPlaying"] = _vm.Deck1.Player.IsPlaying,
        ["Deck1.FilePath"] = _vm.Deck1.Player.CurrentFilePath,
        ["Deck1.PositionFrames"] = _vm.Deck1.Player.PositionFrames,
        ["Deck1.PlayPosition"] = _vm.Deck1.Player.PlayPosition,
        ["Deck1.Volume"] = _vm.Deck1.Player.Volume,
        ["Deck1.TempoPosition"] = _vm.Deck1.Player.TempoPosition,
        ["Deck1.CueActive"] = _vm.Deck1.Player.CueActive,
        ["Deck2.IsLoaded"] = _vm.Deck2.Player.IsLoaded,
        ["Deck2.IsPlaying"] = _vm.Deck2.Player.IsPlaying,
        ["Deck2.FilePath"] = _vm.Deck2.Player.CurrentFilePath,
        ["Deck2.PositionFrames"] = _vm.Deck2.Player.PositionFrames,
        ["Deck2.PlayPosition"] = _vm.Deck2.Player.PlayPosition,
        ["Deck2.Volume"] = _vm.Deck2.Player.Volume,
        ["Deck2.TempoPosition"] = _vm.Deck2.Player.TempoPosition,
        ["Deck2.CueActive"] = _vm.Deck2.Player.CueActive,
    };

    /// <summary>Presses and releases a named <see cref="Key"/> (e.g. "D1", "Space",
    /// "M", "Escape") with no modifiers. <see cref="MainWindow.OnGlobalKeyDown"/>
    /// only reads <c>e.Key</c> and <c>e.KeyModifiers</c> — never the physical-key/
    /// text-symbol pair — so <see cref="PhysicalKey.None"/> and a null symbol are
    /// honest here, not a workaround.</summary>
    private void PressKey(string keyName)
    {
        var key = Enum.Parse<Key>(keyName, ignoreCase: true);
        _window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        _window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Clicks the centre of row <paramref name="index"/> of the named
    /// control. Only "TrackList" (the library <see cref="ListBox"/>, named in
    /// MainWindow.axaml) is wired — the vocabulary stays honest about what this
    /// harness can actually reach today rather than pretending to be general.</summary>
    private void ClickTarget(string target, int index)
    {
        if (target != "TrackList")
            throw new NotSupportedException($"click target \"{target}\" isn't wired — only \"TrackList\" is.");

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

    /// <summary>Renders one frame and writes it as a PNG. Requires the headless
    /// app to have been started with <c>UseHeadlessDrawing = false</c> (see
    /// <see cref="BenchHeadlessApp"/>) — otherwise <c>CaptureRenderedFrame</c>
    /// returns null.</summary>
    public AppearanceCapture Screenshot(string outPath)
    {
        var frame = _window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException(
                "CaptureRenderedFrame returned null — headless drawing must be disabled (UseHeadlessDrawing = false) to get real pixels.");

        string fullPath = Path.GetFullPath(outPath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        using (var fs = File.Create(fullPath))
            frame.Save(fs);

        bool isBlank = IsBlank(frame);
        return new AppearanceCapture
        {
            Path = fullPath,
            Width = frame.PixelSize.Width,
            Height = frame.PixelSize.Height,
            FileSizeBytes = new FileInfo(fullPath).Length,
            IsBlank = isBlank,
        };
    }

    private static unsafe bool IsBlank(Avalonia.Media.Imaging.WriteableBitmap frame)
    {
        using var locked = frame.Lock();
        int byteCount = locked.RowBytes * frame.PixelSize.Height;
        if (byteCount == 0) return true;
        var bytes = new ReadOnlySpan<byte>((void*)locked.Address, byteCount);
        byte first = bytes[0];
        for (int i = 1; i < bytes.Length; i++)
            if (bytes[i] != first) return false;
        return true;
    }
}
