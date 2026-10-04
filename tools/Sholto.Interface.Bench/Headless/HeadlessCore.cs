using Sholto.App;
using Sholto.App.Audio;

namespace Sholto.Interface.Bench.Headless;

/// <summary>What <see cref="IHeadlessCoreFactory.Build"/> builds: the headless core plus the two
/// concrete <see cref="Deck"/>s behind its deck sessions (the core only exposes ports; Bench needs the
/// internals).</summary>
public sealed record HeadlessCore(CoreStack Core, Deck Deck1, Deck Deck2);
