using Sholto.Data;
using Sholto.Interface.MainUI.Models;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>A <see cref="TagEditorViewModel"/> over a real bus: the tag queries answered by
/// <see cref="Queries"/>, the two tag commands recorded on <see cref="Adds"/> and <see cref="Removes"/>, and
/// the App's answer to an add delivered by the test through <see cref="Answer"/> (the editor adds its chip
/// and sets its status line only when that outcome arrives).</summary>
internal sealed class F9FixTagEditorRig
{
    public F9FixTagEditorRig()
    {
        Bus = new DataBus(new ThrowingFailureSink());
        Bus.Register<GetTrackTags, Task<IReadOnlyList<string>>>(Queries);
        Bus.Register<SuggestTags, Task<IReadOnlyList<string>>>(Queries);
        Bus.Register(Adds);
        Bus.Register(Removes);
        Vm = new TagEditorViewModel(Bus, Bus, Bus, new TagRecency());
    }

    public Guid TrackId { get; } = Guid.NewGuid();
    public DataBus Bus { get; }
    public F9FixTagQueryHandlers Queries { get; } = new();
    public F9FixRecordingCommandHandler<AddTagToTrack> Adds { get; } = new();
    public F9FixRecordingCommandHandler<RemoveTagFromTrack> Removes { get; } = new();
    public TagEditorViewModel Vm { get; }

    /// <summary>The App reports what became of the last add, for this rig's track.</summary>
    public void Answer(TagAddOutcome outcome, string? storedName, int limit = 0) =>
        Bus.Publish(new TagAddAttempted(TrackId, outcome, storedName, limit));
}
