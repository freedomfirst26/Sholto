using Sholto.Interface.Keyboard;
using Sholto.Interface.Controller;
using Sholto.App;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI;

/// <summary>Abstract factory for the entities the composition root wires
/// together: a control surface, a keyboard, the main view model, and the headless core they all act on.
/// The input stack binds the surface and the core; the keyboard raises raw key events that the root
/// recognises into gestures. They are created
/// through ONE factory rather than three separate `new` calls because they have to
/// be consistent with each other — the keyboard must be the same window that is
/// showing the application this factory returns, or a keypress and a controller
/// press act on two different objects. That consistency is the factory's job and
/// is unenforceable when the composition root news each one up in a different
/// method (which is exactly what <c>App</c> used to do, ending in a
/// <c>(IKeyboard)desktop.MainWindow!</c> cast that nothing guaranteed).
///
/// <para><b>Scope.</b> The factory assembles the ENTITIES only. It does not
/// build the ~20 leaf collaborators (tool options, ToolSet, the analysers, decoder,
/// storage, theme, scanner, MIDI manager, mappings) — those stay in <c>App</c>'s
/// <c>InitializeServices</c> / <c>OnFrameworkInitializationCompleted</c>. Three
/// layers, in order: composition root builds leaves → factory assembles entities →
/// the input stack binds them. Pulling the leaves in here would recreate
/// the god object this refactor exists to dismantle.</para>
///
/// <para><see cref="LiveEntities"/> is the real one (FLX4, real window, real
/// <c>MainViewModel</c>). <c>Sholto.Interface.Bench</c> is the other intended implementer:
/// a scripted surface and a scripted keyboard over the same real app.</para></summary>
public interface ISholtoEntities
{
    IControlSurface CreateControlSurface();
    IKeyboard       CreateKeyboard();
    MainViewModel   CreateApplication();

    /// <summary>The headless core: decks, mixer, library, markers, loader. The view model projects it.</summary>
    CoreStack       CreateCore();
}
