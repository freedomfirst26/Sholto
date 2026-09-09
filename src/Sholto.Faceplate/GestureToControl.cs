using System.Runtime.CompilerServices;
using Sholto.Faceplate.Model;

namespace Sholto.Faceplate;

/// <summary>Resolves a live gesture id to the control that owns it in a guide document.
/// <para>This is called at knob-turn rates (every tick of a jog or fader), so the
/// id→control map is built once per document — via <see cref="ConditionalWeakTable{TKey,TValue}"/>,
/// keyed on the document instance — rather than rescanned on every gesture.</para></summary>
public static class GestureToControl
{
    private static readonly ConditionalWeakTable<FaceplateDoc, Dictionary<string, string>> Cache = new();

    /// <summary>The id of the control whose <c>Gestures</c> contain <paramref name="gestureId"/>,
    /// or null when no control claims it.
    /// <para>A handful of ids are shared by more than one control — the three EQ knobs all
    /// answer to "eq.turn" — and for those this returns whichever control listed it first
    /// in the document. A caller that can disambiguate further from the source event (the
    /// EQ knob carries its own band) should do so itself rather than relying on this.</para>
    /// </summary>
    public static string? Resolve(FaceplateDoc doc, string gestureId) =>
        Cache.GetValue(doc, BuildMap).GetValueOrDefault(gestureId);

    private static Dictionary<string, string> BuildMap(FaceplateDoc doc)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var control in doc.Controls)
            foreach (var gesture in control.Gestures)
                map.TryAdd(gesture.Id, control.Id);
        return map;
    }
}
