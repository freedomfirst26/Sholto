using Sholto.App;
using Sholto.App.Audio;
using Sholto.Interface.Bench.Controller;

namespace Sholto.Interface.Bench.Headless;

/// <summary>One mounted headless run: the core, its two concrete decks, the controller-side pipeline
/// and the driver that handles the scenario's gesture/midi/scan steps.</summary>
public sealed record HeadlessSession(CoreStack Core, Deck Deck1, Deck Deck2, GestureHost Gestures, HeadlessScenarioDriver Driver);
