using Sholto.Data;

namespace Sholto.Interface.MainUI.Controls.CollapseToIcon;

public sealed class HintPolicyFactory(IQueryAsker asker, ICommandSender sender) : IHintPolicyFactory
{
    private readonly IQueryAsker _asker = asker;
    private readonly ICommandSender _sender = sender;

    public IHintPolicy Always() => new AlwaysHintPolicy();

    public IHintPolicy Never() => new NeverHintPolicy();

    public IHintPolicy FirstDismissals(string key, int count) =>
        new FirstDismissalsHintPolicy(count, new BusHintCounter(_asker, _sender, key));
}
