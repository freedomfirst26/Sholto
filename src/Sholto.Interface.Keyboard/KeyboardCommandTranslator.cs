using Sholto.Data;

namespace Sholto.Interface.Keyboard;

/// <summary>P plays or pauses, 1 and 2 load the highlighted track, M drops a marker, G opens the grid
/// editor, Ctrl+Z undoes the last load. Each command is stamped with the key it came from.</summary>
internal sealed class KeyboardCommandTranslator(ICommandSender sender) : IKeyboardCommandTranslator
{
    private readonly ICommandSender _sender = sender;

    public void Translate(in KeyboardGesture g)
    {
        var origin = new Origin(InterfaceIds.Keyboard, g.ControlId, g.Id);
        switch (g.Id)
        {
            case KeyboardGestureIds.PlayPress: _sender.Send(new TogglePlay(g.Deck, origin)); break;
            case KeyboardGestureIds.LoadPress: _sender.Send(new LoadSelectedIntoDeck(g.Deck, origin)); break;
            case KeyboardGestureIds.MarkerAdd: _sender.Send(new AddMarker(g.Deck, origin)); break;
            case KeyboardGestureIds.GridEditOpen: _sender.Send(new OpenGridEditor(origin)); break;
            case KeyboardGestureIds.LoadUndo: _sender.Send(new UndoLastLoad(origin)); break;
        }
    }
}
