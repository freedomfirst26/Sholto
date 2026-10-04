using Sholto.Data;

namespace Sholto.App.Performance;

/// <summary>The platter commands: the one place that decides whether a turn is a scratch or a silent seek.</summary>
public interface IPlatter : ICommandHandler<TouchPlatter>, ICommandHandler<TurnPlatter>
{
}
