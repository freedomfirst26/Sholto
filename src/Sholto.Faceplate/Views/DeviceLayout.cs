using Avalonia;
using Avalonia.Controls;

namespace Sholto.Faceplate.Views;

/// <summary>Marks a shape in a device layout as a named control.
/// <para>A device layout is pure drawing: shapes, and these three attached properties.
/// Generic code walks the tree, finds everything carrying an <see cref="IdProperty"/>,
/// and wires hover, selection, the warm glow and the unused fill. Adding a controller
/// therefore needs a new .axaml and a new .json, and no C# at all.</para></summary>
public static class DeviceLayout
{
    /// <summary>The control id. Must match an id in the device's guide JSON.</summary>
    public static readonly AttachedProperty<string?> IdProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Id", typeof(DeviceLayout));

    /// <summary>0 or 1 for a per-deck control; -1 for a global one. Two shapes share
    /// one id when a control exists on both decks — the deck tells them apart.</summary>
    public static readonly AttachedProperty<int> DeckProperty =
        AvaloniaProperty.RegisterAttached<Control, int>("Deck", typeof(DeviceLayout), defaultValue: -1);

    /// <summary>"A click or a hover here means the control with THIS id." Put on a
    /// printed legend — the little silk-screened box above or below a button — so the
    /// label answers the pointer on behalf of the control it names.
    /// <para>Why this is not simply <see cref="IdProperty"/>: the overlay keys its
    /// shapes by (id, deck) and each key owns the one shape that lights. Two shapes
    /// claiming one key would collide and one would win arbitrarily. A legend is
    /// therefore a <i>hit proxy</i>: it forwards hover and click, is never registered as
    /// a shape, and the control's own body stays the thing that glows.</para>
    /// <para>Scaled into a window a 36px button is roughly 20 screen pixels while its
    /// legend is wider and more obvious, so for many controls the label was both the
    /// larger target and the dead one.</para>
    /// <para><see cref="DeckProperty"/> on the proxy says which deck it speaks for. Left
    /// unset, the id is resolved on whichever deck is live, exactly as a prose chip in
    /// the panel is.</para></summary>
    public static readonly AttachedProperty<string?> HitForProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("HitFor", typeof(DeviceLayout));

    public static string? GetId(Control c) => c.GetValue(IdProperty);
    public static void SetId(Control c, string? v) => c.SetValue(IdProperty, v);
    public static int GetDeck(Control c) => c.GetValue(DeckProperty);
    public static void SetDeck(Control c, int v) => c.SetValue(DeckProperty, v);
    public static string? GetHitFor(Control c) => c.GetValue(HitForProperty);
    public static void SetHitFor(Control c, string? v) => c.SetValue(HitForProperty, v);
}
