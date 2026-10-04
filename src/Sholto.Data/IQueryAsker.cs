namespace Sholto.Data;

/// <summary>Interface-facing: ask a query. If no handler is registered, or the handler throws, the
/// failure is reported to <see cref="IHandlerFailureSink"/> and <c>default</c> is returned.</summary>
public interface IQueryAsker
{
    TR Ask<TQ, TR>(in TQ query) where TQ : struct, IQuery<TR>;
}
