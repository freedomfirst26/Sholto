namespace Sholto.Interface.MainUI.Controls.Modal;

/// <summary>What a press on the scrim behind a modal does.</summary>
public enum ModalScrimClick
{
    /// <summary>It dismisses the modal.</summary>
    Dismisses,

    /// <summary>It does nothing: the app needs an answer from the modal.</summary>
    Ignored,
}
