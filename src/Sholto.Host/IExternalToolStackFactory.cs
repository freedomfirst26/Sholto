namespace Sholto.Host;

/// <summary>Builds the <see cref="ExternalToolStack"/> once at startup.</summary>
public interface IExternalToolStackFactory
{
    ExternalToolStack Build();
}
