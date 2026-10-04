namespace Sholto.Interface.MainUI;

/// <summary>Builds the <see cref="ExternalToolStack"/> once at startup.</summary>
public interface IExternalToolStackFactory
{
    ExternalToolStack Build();
}
