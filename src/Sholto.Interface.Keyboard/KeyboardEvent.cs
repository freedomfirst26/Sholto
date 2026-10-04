using Avalonia.Input;

namespace Sholto.Interface.Keyboard;

/// <summary>A raw key press: which key, with which modifiers.</summary>
public sealed record KeyboardEvent(Key Key, KeyModifiers Modifiers);
