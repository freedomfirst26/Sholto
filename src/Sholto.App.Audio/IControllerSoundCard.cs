namespace Sholto.App.Audio;

/// <summary>Recognises the DJ controller's built-in sound card by name, to tell its
/// playback device / PipeWire sink apart from ordinary speakers.</summary>
public interface IControllerSoundCard
{
    /// <summary>True if the node/description name matches the controller's card.
    /// False for null/empty.</summary>
    bool Matches(string? nodeOrDesc);
}
