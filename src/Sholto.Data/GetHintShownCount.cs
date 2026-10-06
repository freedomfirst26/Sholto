namespace Sholto.Data;

/// <summary>How many times the hint named <see cref="HintKey"/> has been shown, across launches (see
/// <see cref="RecordHintShown"/>). 0 for a key never shown or outside <c>[a-z_]+</c>.</summary>
public readonly record struct GetHintShownCount(string HintKey) : IQuery<Task<int>>;
