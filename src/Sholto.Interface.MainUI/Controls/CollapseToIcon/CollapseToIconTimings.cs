namespace Sholto.Interface.MainUI.Controls.CollapseToIcon;

/// <summary>How long each phase of a collapse-to-icon sequence lasts.</summary>
/// <param name="Collapse">The shrink into the icon.</param>
/// <param name="Grow">The growth back out of it.</param>
/// <param name="PulsePeriod">One pulse of the hint.</param>
/// <param name="PulseCount">How many pulses the hint runs.</param>
/// <param name="ReducedFade">The plain fade that replaces shrink and grow under reduced motion.</param>
/// <param name="ReducedHintHold">How long the still hint outline stays under reduced motion.</param>
public sealed record CollapseToIconTimings(
    TimeSpan Collapse,
    TimeSpan Grow,
    TimeSpan PulsePeriod,
    int PulseCount,
    TimeSpan ReducedFade,
    TimeSpan ReducedHintHold);
