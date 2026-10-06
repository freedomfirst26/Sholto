using Sholto.App.Audio;

namespace Sholto.TestSupport;

/// <summary>A stem control that records what it is asked to push to the audio, in order.</summary>
internal sealed class SpyStemControl : IStemControl
{
    public List<(int Group, bool Active)> Mutes { get; } = [];

    public List<(int Group, double Level)> Levels { get; } = [];

    public void SetStemGroup(int group, bool active) => Mutes.Add((group, active));

    public void SetStemGroupLevel(int group, double level) => Levels.Add((group, level));
}
