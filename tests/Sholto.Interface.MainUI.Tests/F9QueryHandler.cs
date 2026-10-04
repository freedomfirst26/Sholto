using Sholto.Data;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>A query handler answered by a function the test supplies; it keeps every query it was asked.</summary>
internal sealed class F9QueryHandler<TQ, TR>(Func<TQ, TR> answer) : IQueryHandler<TQ, TR> where TQ : struct, IQuery<TR>
{
    private readonly Func<TQ, TR> _answer = answer;

    public List<TQ> Asked { get; } = [];

    public TR Handle(in TQ query)
    {
        Asked.Add(query);
        return _answer(query);
    }
}
