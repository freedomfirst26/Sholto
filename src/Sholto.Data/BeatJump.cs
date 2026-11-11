namespace Sholto.Data;

/// <summary>Move the playhead by <see cref="Beats"/> beats of the track's BPM (negative = back).</summary>
public readonly record struct BeatJump(int Deck, double Beats, Origin Origin) : ICommand;
