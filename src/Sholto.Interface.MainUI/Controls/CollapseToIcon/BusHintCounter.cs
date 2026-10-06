using Sholto.Data;

namespace Sholto.Interface.MainUI.Controls.CollapseToIcon;

/// <summary>The saved hint count for one key, kept by the App and reached over the bus.</summary>
public sealed class BusHintCounter(IQueryAsker asker, ICommandSender sender, string key) : IHintCounter
{
    private readonly IQueryAsker _asker = asker;
    private readonly ICommandSender _sender = sender;
    private readonly string _key = key;

    public Task<int> CountAsync() => _asker.AskAsync<GetHintShownCount, int>(new GetHintShownCount(_key));

    public void Record() =>
        _sender.Send(new RecordHintShown(_key, new Origin(InterfaceIds.MainUI, "hint-" + _key, "shown")));
}
