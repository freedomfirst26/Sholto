using Sholto.App.Decks;

namespace Sholto.App;

/// <summary>Builds the <see cref="CoreStack"/> over the two deck sessions.</summary>
public interface ICoreFactory
{
    CoreStack Build(IDeckSession deck1, IDeckSession deck2);
}
