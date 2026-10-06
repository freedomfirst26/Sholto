namespace Sholto.Interface.Keyboard;

/// <summary>The keyboard's gestures. The first two share their ids with the controller's PLAY and LOAD
/// buttons (the guide describes them by id); the others exist only on the keyboard.</summary>
internal static class KeyboardGestureIds
{
    public const string PlayPress = "play.press";
    public const string LoadPress = "load.press";
    public const string MarkerAdd = "marker.add";
    public const string GridEditOpen = "gridedit.open";
    public const string LoadUndo = "load.undo";
}
