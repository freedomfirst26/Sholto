using Avalonia.Media;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>Every colour a <c>RotaryKnob</c> draws. <see cref="Arc"/> is the theme key <c>knob.arc</c> (the value
/// ring and the ticks it has reached, green by default); the rest come from the theme's core colours: the
/// unlit track and cap edge from border, the cap from surfaceRaised, unlit ticks and labels from textMuted,
/// the pointer from textBright.</summary>
public sealed record KnobPalette(Color Arc, Color Track, Color Cap, Color CapEdge, Color Tick, Color Pointer);
