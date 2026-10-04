namespace Sholto.Data;

/// <summary>Answers query <typeparamref name="TQ"/>. Runs synchronously on the caller's thread.</summary>
public interface IQueryHandler<TQ, TR> where TQ : struct, IQuery<TR>
{
    TR Handle(in TQ query);
}
