namespace Sholto.Interface.Controller.Gestures;

/// <summary>The set of every gesture id the recognizer can emit. Built once at
/// bootstrap and handed to whatever needs to enumerate them (the faceplate
/// bindings). The id strings themselves stay as consts on <see cref="GestureIds"/>.</summary>
internal interface IGestureCatalog
{
    IReadOnlyList<string> All { get; }
}
