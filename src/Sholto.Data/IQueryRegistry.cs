namespace Sholto.Data;

/// <summary>App-facing: register the single handler for a query type. A second registration throws.</summary>
public interface IQueryRegistry
{
    void Register<TQ, TR>(IQueryHandler<TQ, TR> handler) where TQ : struct, IQuery<TR>;
}
