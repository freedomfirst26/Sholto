using Sholto.App;
using Sholto.App.Audio;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.Interface.Bench.Headless;

/// <summary>What <see cref="IHeadlessCoreFactory.Build"/> builds: the headless core plus the two
/// concrete <see cref="Deck"/>s behind its deck sessions (the core only exposes ports; Bench needs the
/// internals) and the offline engine they are attached to (what a headless render pulls through).</summary>
public sealed record HeadlessCore(CoreStack Core, Deck Deck1, Deck Deck2, SfEngine Engine);
