# Adding a controller

A controller needs **exactly two files**, and no C# at all:

```
Devices/<Device>/<device>.guide.json     what each control does
Devices/<Device>/<Device>Layout.axaml    what it looks like
```

(The `.axaml` needs a three-line `.axaml.cs` beside it, because `x:Class` has to
attach to a partial class. Nothing goes in it.)

## The drawing

The root is a `Canvas` sized to the reference render's own pixel space — the
DDJ-FLX4 uses `1792 x 1316` — so a coordinate you measure off a top-down photo of
the unit is a coordinate you type. A `Viewbox` scales the whole thing at display
time, so those numbers never change again.

Shapes are plain Avalonia primitives: `Ellipse`, `Border`, `Rectangle`, `Line`,
`Polygon`, `TextBlock`. Do not reach for `ICustomDrawOperation` or a Skia lease;
they have repeatedly failed to composite in nested layouts in this app.

## The two attached properties

Both live in `Sholto.Faceplate.Views.DeviceLayout`:

| Property | Meaning |
|---|---|
| `fp:DeviceLayout.Id` | The control id. Must match an id in the guide JSON. |
| `fp:DeviceLayout.Deck` | `0` or `1` for a per-deck control, `-1` for a global one. |

Two shapes share one id when the control exists on both decks; the deck tells
them apart. Generic code walks the tree, finds everything carrying an `Id`, and
wires hover, selection, the warm glow and the unused fill — so a shape becomes
interactive purely by carrying the property.

Ids must be ids the recognizer can actually emit: they are checked against
`Sholto.Controller.Gestures.GestureIds`.

Put the `Id` on the **outermost** shape of a control — the knob's skirt, the
fader's cap, the pad's border — and mark every decorative shape drawn on top of
it `IsHitTestVisible="False"`, or the decoration swallows the hover.

## The tests keep the two files honest

`tests/Sholto.Faceplate.Tests/FaceplateDocTests.cs` reads the `.axaml` as plain
XML (no Avalonia runtime needed) and fails **by name** in both directions:

- `Every_control_in_the_data_file_has_a_shape_in_the_layout` — you described a
  control but never drew it.
- `Every_shape_in_the_layout_is_described` — you drew a shape whose id has no
  entry in the guide JSON.

So the workflow is: add the control to the JSON, run the tests, and let the
failure tell you what is still missing from the drawing.
