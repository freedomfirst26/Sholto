using Avalonia.Input;

namespace Sholto.Interface.MainUI.Controls.Modal;

/// <summary>Runs the one standard key order for an open modal.</summary>
public interface IModalKeyRouter
{
    /// <summary>Routes one key to an open modal. True means the key is consumed and the caller sets
    /// <c>e.Handled</c>.</summary>
    bool Route(IModal modal, Key key, KeyModifiers modifiers);
}
