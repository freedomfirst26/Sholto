namespace Sholto.App.Audio;

/// <summary>Recognises the built-in sound card of whichever DJ controller(s) are
/// supported, by name fragments supplied by the controller mappings. Used to tell
/// the controller's playback device / PipeWire sink apart from ordinary speakers.</summary>
public sealed class ControllerSoundCard(IReadOnlyList<string> nameFragments) : IControllerSoundCard
{
    private readonly IReadOnlyList<string> _nameFragments = nameFragments;

    /// <summary>True if the node/description name contains any fragment
    /// (case-insensitive). False for null/empty.</summary>
    public bool Matches(string? nodeOrDesc)
    {
        if (string.IsNullOrEmpty(nodeOrDesc)) return false;
        foreach (var fragment in _nameFragments)
            if (!string.IsNullOrEmpty(fragment) && nodeOrDesc.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
