namespace Sholto.Interface.MainUI.Harness.Ui;

/// <summary>Starts the headless Avalonia application the <c>ui</c>/<c>screenshot</c> subcommands need.</summary>
public interface IBenchHeadlessApp
{
    /// <summary>Starts Avalonia on the first call; later calls on the same instance do nothing.</summary>
    void EnsureStarted();
}
