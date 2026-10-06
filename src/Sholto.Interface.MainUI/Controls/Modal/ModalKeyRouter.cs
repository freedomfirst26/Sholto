using Avalonia.Input;

namespace Sholto.Interface.MainUI.Controls.Modal;

/// <summary>The standard key order for an open modal, stateless: the content's own keys, then Esc dismisses,
/// then Enter confirms (Primary slot and <c>CanConfirm</c>), then Backspace goes back (Back slot and
/// <c>CanGoBack</c>), then everything else is swallowed unless the content captures text.</summary>
public sealed class ModalKeyRouter : IModalKeyRouter
{
    public bool Route(IModal modal, Key key, KeyModifiers modifiers)
    {
        if (modal.HandleKey(key, modifiers)) return true;

        if (key == Key.Escape)
        {
            modal.Dismiss();
            return true;
        }

        if (modal is IModalContent content)
        {
            if (key == Key.Enter && content.Buttons.Primary is not null)
            {
                if (content.CanConfirm) content.Confirm();
                return true;
            }

            if (key == Key.Back && content.Buttons.Back is not null)
            {
                if (content.CanGoBack)
                {
                    content.Back();
                    return true;
                }
                return !content.CapturesText;
            }
        }

        return !modal.CapturesText;
    }
}
