namespace Sholto.Data;

/// <summary>App-facing: register the single handler for a command type. A second registration throws.</summary>
public interface ICommandRegistry
{
    void Register<T>(ICommandHandler<T> handler) where T : struct, ICommand;
}
