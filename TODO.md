# TODO

- [ ] **First-run tutorial.** The first time Sholto opens, walk the user through the app
      rather than leaving them to find it. It should introduce each part of the screen —
      the library, the decks, the waveforms, search, crates and tags — and open the
      **Faceplate at full size** as the controller chapter, so they meet the controller
      guide instead of discovering it by accident.
      Why it matters now: the README's how-to sections are being deleted because the
      Faceplate documents the controller better. But the Faceplate covers the CONTROLLER
      only. Until this tutorial exists, nothing teaches the keyboard shortcuts or the
      on-screen moves (clicking the BPM to open the tuner, double-clicking to re-analyse,
      pressing M to drop a marker). This is the thing that closes that gap.
      Backlog — not part of the Faceplate work.

- [ ] **A Faceplate for the keyboard.** The keyboard is an input device like the controller,
      and it deserves the same treatment: a picture of it, every bound key explained, and
      the key lighting up when you press it.
      The device contract already supports this — a device is a layout `.axaml` of shapes
      carrying `ControlSurface.Id`, plus a `.json` of descriptions keyed by gesture id, and
      no C# at all. See `src/Sholto.Faceplate/Devices/README.md`.
      **The one real piece of work** is that keyboard presses do not currently become
      gestures. Controller input flows MIDI -> `DdjFlx4Mapping` -> `ControllerEvent` ->
      `GestureRecognizer` -> `Gesture` -> `GestureBus`, but keyboard shortcuts are handled
      directly in `MainWindow.axaml.cs`'s key handler and never enter that pipeline. So this
      needs a keyboard gesture source feeding the same bus. Once it does, the guide, the
      live highlighting and the blink all work with no new UI code — and the keyboard
      shortcuts stop being duplicated between the key handler and the README.
      Backlog — not part of the current Faceplate work.

- [ ] **A shared modal shell for every overlay.** Sholto has five overlays — search, the tag
      editor, the track actions menu, the crate picker and now the Faceplate — and each one
      re-implements its own backdrop, its own click-outside-to-close, its own Esc handling
      and its own frame. They do not agree: some have a visible close button, some only
      answer to Esc, and the backdrop scrim differs between them.
      Build one `ModalShell` control that owns the backdrop, the frame, a visible X in the
      top right, Esc, click-outside, focus trapping and the open/close transition. Then move
      the five overlays onto it.
      Why it is worth doing: a user should not have to learn a different way out of each
      overlay, and a visible X should not be something each overlay remembers to add — the
      Faceplate only got one because the user asked. It also deletes five copies of the same
      backdrop and key-handling code.
      Backlog — not part of the Faceplate work.

- [ ] **Dissolve `Orchestrator.HandleGesture`'s switch into the gesture binding table.**
      `Orchestrator.cs` is ~703 lines doing three jobs: a 34-arm switch turning gestures into
      app actions (~195 lines), the scratch engine (~276 lines), and the 60 Hz frame pump.
      The mechanism to fix this already exists — `GestureBindings` takes a
      `Dictionary<string, Action<Gesture>>`. Today `App.axaml.cs:287` builds it as a
      placeholder: all 34 ids point at the single `HandleGesture` method.
      Replace that with one named lambda per gesture, assembled from five small classes
      grouped by what they actually touch: `TransportBindings` (play, both CUEs, sync),
      `MixerBindings` (EQ, stem levels, filter, volume, tempo, crossfader), `PadBindings`
      (stem pads, echo, pad modes), `LoopBindings` (beat loop, grid nudge) and
      `BrowseBindings` (turn, tap, hold, load). The switch then disappears.
      **Three things this unlocks, which are the real reason to do it:**
      1. Coverage becomes a test — `Assert.Empty(GestureIds.All.Except(table.BoundIds))`.
         Today that is unknowable; a reviewer already found the switch's `default:` arm is
         unreachable and nobody can currently assert it.
      2. Each group becomes testable alone with a fake deck. Those 34 cases have no direct
         tests today.
      3. The Orchestrator stops being a god object, leaving the scratch engine as an obvious
         second extraction — 276 self-contained lines that are the riskiest and least tested
         code in the app, and the only place where a regression is felt rather than caught.
      Do it on its own branch, after the Faceplate work is committed. It touches the audio
      path that two Faceplate tasks went out of their way not to disturb, so it must not
      share a diff with a UI feature.
