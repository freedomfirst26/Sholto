using Avalonia.Input;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Views;

/// <summary>The query box's key routing into the Glance view model, and the text swallow that goes with Shift+1/Shift+2.</summary>
internal interface ISearchOverlayKeyHandler
{
    /// <summary>Routes a key pressed in the box; marks it handled when it did something.</summary>
    void OnKeyDown(IGlanceViewModel? glance, KeyEventArgs e);

    /// <summary>Swallows the character the previous Shift+1/Shift+2 would have typed.</summary>
    void OnTextInput(TextInputEventArgs e);

    /// <summary>The caret sits at the end of the text with nothing selected.</summary>
    bool CaretAtEndNoSelection();
}
