namespace Sholto.Data;

/// <summary>The folder Sholto writes its analysis output (stems) to. State. Published once the folder is in
/// use, including after a move of the existing stems; closes the folders question.</summary>
/// <param name="Path">The analysis folder now in use.</param>
/// <param name="IsOverride">True when an environment variable sets it, so the user cannot change it and it is not saved.</param>
public readonly record struct AnalysisFolderChanged(string Path, bool IsOverride) : IStateEvent
{
    public int Slot => 0;
}
