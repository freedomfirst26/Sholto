namespace Sholto.Interface.MainUI.Controls.Modal;

/// <summary>Which button slots a modal has and their labels. A null slot is absent. <see cref="Dismiss"/> is
/// always present: ghost on the left when there is a <see cref="Primary"/>, secondary on the right when
/// there is not.</summary>
public sealed record ModalButtons(string Dismiss, string? Back, string? Primary);
