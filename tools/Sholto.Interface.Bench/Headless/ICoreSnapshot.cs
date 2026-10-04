using Sholto.App;
using Sholto.App.Audio;

namespace Sholto.Interface.Bench.Headless;

/// <summary>Reads the small set of observable core/deck fields a scenario step is diffed on.</summary>
public interface ICoreSnapshot
{
    Dictionary<string, object?> Take(CoreStack core, IReadOnlyList<Deck> decks);
}
