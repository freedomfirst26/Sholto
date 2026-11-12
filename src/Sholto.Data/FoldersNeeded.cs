namespace Sholto.Data;

/// <summary>The App needs the user to look at the music and analysis folders. A fact: not replayed. An
/// interface that can ask shows both rows, each changeable on its own, and answers with
/// <see cref="ChooseFolders"/> (both paths null when cancelled).</summary>
/// <param name="Reason">Why the App is asking, so the interface can word the prompt.</param>
/// <param name="Music">The music folder row.</param>
/// <param name="Analysis">The analysis folder row (where stems are written).</param>
/// <param name="AnalysisStemBytes">Bytes of stems already in the analysis folder, to show what a change would move; 0 when none.</param>
public readonly record struct FoldersNeeded(FoldersReason Reason, FolderRow Music, FolderRow Analysis, long AnalysisStemBytes) : IEvent;
