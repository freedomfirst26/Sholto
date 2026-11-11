using Sholto.Interface.Controller;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Host;

/// <summary>The leaves the composition root hands a <see cref="LiveEntities"/> once it has built
/// them, because the entity factory is created before any leaf exists. Deliberately NOT on
/// <see cref="ISholtoEntities"/>: that interface stays the Create… methods, and a scripted
/// world brings its own seeds (or a no-op implementation of this one).</summary>
public interface ILiveEntitySeeds
{
    /// <summary>The theme catalog the view model and window start from.</summary>
    void UseThemeCatalog(IThemeCatalog themeCatalog);

    /// <summary>The leaves the real application is built from.</summary>
    void UseViewModelStack(ViewModelStack leaves);

    /// <summary>The MIDI stack the real control surface talks through.</summary>
    void UseMidi(IMidiConnection midi);
}
