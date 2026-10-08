namespace Sholto.Data;

/// <summary>Highlight a library row. With <see cref="Clamp"/> the index is clamped to the visible rows (arrow keys); without it, it is taken as given (the list box reports -1 when its items are cleared).</summary>
public readonly record struct SelectTrack(int Index, bool Clamp, Origin Origin) : ICommand;
