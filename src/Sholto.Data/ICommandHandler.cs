namespace Sholto.Data;

/// <summary>Handles command <typeparamref name="T"/>. Runs synchronously on the caller's thread.</summary>
public interface ICommandHandler<T> where T : struct, ICommand
{
    void Handle(in T command);
}
