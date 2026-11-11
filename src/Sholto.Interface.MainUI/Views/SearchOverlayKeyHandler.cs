using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Views;

/// <inheritdoc cref="ISearchOverlayKeyHandler"/>
internal sealed class SearchOverlayKeyHandler(TextBox queryBox) : ISearchOverlayKeyHandler
{
    private const int PageSize = 8;

    private readonly TextBox _queryBox = queryBox;
    private bool _swallowNextText;

    public bool CaretAtEndNoSelection()
    {
        var length = _queryBox.Text?.Length ?? 0;
        return _queryBox.CaretIndex == length && _queryBox.SelectionStart == _queryBox.SelectionEnd;
    }

    public void OnKeyDown(IGlanceViewModel? glance, KeyEventArgs e)
    {
        // A swallow set by the previous key never outlives it, whether or not text input followed.
        _swallowNextText = false;
        if (glance is null) return;

        if (e.KeyModifiers == KeyModifiers.Shift && e.Key is Key.D1 or Key.NumPad1 or Key.D2 or Key.NumPad2)
        {
            // The character this key would type ("!", "@", or a layout's own) must not reach the box.
            _swallowNextText = true;
            glance.LoadTo(e.Key is Key.D1 or Key.NumPad1 ? 0 : 1);
            e.Handled = true;
            return;
        }
        // Ctrl+L adds to the Track List and Ctrl+Delete clears it (twice); before the modifier return below.
        // Ctrl+Delete would otherwise delete the word ahead of the caret.
        if (e.KeyModifiers == KeyModifiers.Control && e.Key is Key.L or Key.Delete)
        {
            if (e.Key == Key.L) glance.LoadToTrackList();
            else glance.ClearTrackList();
            e.Handled = true;
            return;
        }
        if (e.KeyModifiers != KeyModifiers.None) return;

        switch (e.Key)
        {
            case Key.Escape: glance.Close(); break;
            case Key.Down: glance.Move(+1); break;
            case Key.Up: glance.Move(-1); break;
            case Key.PageDown: glance.Move(+PageSize); break;
            case Key.PageUp: glance.Move(-PageSize); break;
            case Key.Tab:
                if (CaretAtEndNoSelection() && glance.AcceptTagCompletion())
                    Dispatcher.UIThread.Post(() => _queryBox.CaretIndex = _queryBox.Text?.Length ?? 0);
                else
                    glance.ToggleZone();
                break;
            case Key.Left:
            case Key.Right: glance.FlipTarget(); break;
            case Key.Enter: glance.Activate(); break;
            // In an empty box Backspace takes the newest chip off; with text or no chip it edits as usual.
            case Key.Back:
                if (!glance.RemoveLastChip()) return;
                break;
            default: return;
        }
        e.Handled = true;
    }

    public void OnTextInput(TextInputEventArgs e)
    {
        if (!_swallowNextText) return;
        _swallowNextText = false;
        e.Handled = true;
    }
}
