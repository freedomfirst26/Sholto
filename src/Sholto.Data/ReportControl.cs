namespace Sholto.Data;

/// <summary>"The DJ used this control", for a control with no effect on the App: SHIFT and the
/// stem-level hold (modifiers the interface keeps for itself), a plain CUE or SYNC press, a short browse
/// press. Its handler does nothing; it exists so that, in Inspect mode, the control is echoed as
/// <see cref="CommandReceived"/> like any other and the guide can explain it.
/// <para><see cref="Pressed"/> is the edge: true on press, false on release. A modifier's release is
/// stamped with a distinct gesture name (for example <c>shift.release</c>) on its
/// <see cref="Origin"/>, because the echo carries no payload.</para></summary>
public readonly record struct ReportControl(int Deck, bool Pressed, Origin Origin) : ICommand;
