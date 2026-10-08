namespace Sholto.Data;

/// <summary>A one-time hint was just shown for <see cref="HintKey"/> (lowercase letters and underscores, e.g.
/// <c>faceplate</c>): the App adds one to that key's saved count, read back with <see cref="GetHintShownCount"/>.
/// A key outside <c>[a-z_]+</c> is ignored.</summary>
public readonly record struct RecordHintShown(string HintKey, Origin Origin) : ICommand;
