namespace Sholto.Interface.MainUI.Controls.Modal;

/// <summary>What <see cref="ModalShell"/>'s chrome shows, and what its buttons do. Raise
/// <c>PropertyChanged</c> for any property that changes while open (a wizard changing step).</summary>
public interface IModalContent : IModal
{
    /// <summary>The small caps line above the title, e.g. "SETTINGS".</summary>
    string Eyebrow { get; }

    ModalTone Tone { get; }

    string Title { get; }

    /// <summary>Null means no subtitle row.</summary>
    string? Subtitle { get; }

    string KeyHint { get; }

    ModalWidth Width { get; }

    ModalButtons Buttons { get; }

    /// <summary>The Back slot is enabled.</summary>
    bool CanGoBack { get; }

    /// <summary>The Primary slot is enabled, and so is Enter.</summary>
    bool CanConfirm { get; }

    void Back();

    void Confirm();
}
