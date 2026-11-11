namespace Sholto.App.ExternalTools;

/// <summary>Builds the external-tool options.</summary>
public interface IExternalToolOptionsFactory
{
    ExternalToolOptions FromEnvironment();
    ExternalToolOptions ForCurrentPlatform();
    ExternalToolOptions Linux();
    ExternalToolOptions Windows();
}
