namespace Sholto.Data;

/// <summary>Interface-facing: send a command. If no handler is registered the failure is reported to
/// <see cref="IHandlerFailureSink"/> and nothing is thrown.</summary>
public interface ICommandSender
{
    void Send<T>(in T command) where T : struct, ICommand;
}
