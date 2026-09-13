using Sholto.Controller;

namespace Sholto.App;

/// <summary>Abstract factory for the three entities <see cref="Orchestrator"/> binds
/// together: a control surface, a keyboard, and the app itself. They are created
/// through ONE factory rather than three separate `new` calls because they have to
/// be consistent with each other — the keyboard must be the same window that is
/// showing the application this factory returns, or a keypress and a controller
/// press act on two different objects. That consistency is the factory's job and
/// is unenforceable when the composition root news each one up in a different
/// method (which is exactly what <c>App</c> used to do, ending in a
/// <c>(IKeyboard)desktop.MainWindow!</c> cast that nothing guaranteed).
///
/// <para><b>Scope.</b> The factory assembles the three ENTITIES only. It does not
/// build the ~20 leaf collaborators (tool options, ToolSet, the analysers, decoder,
/// storage, theme, scanner, MIDI manager, mappings) — those stay in <c>App</c>'s
/// <c>InitializeServices</c> / <c>OnFrameworkInitializationCompleted</c>. Three
/// layers, in order: composition root builds leaves → factory assembles entities →
/// <see cref="Orchestrator"/> binds them. Pulling the leaves in here would recreate
/// the god object this refactor exists to dismantle.</para>
///
/// <para><see cref="LiveEntities"/> is the real one (FLX4, real window, real
/// <c>MainViewModel</c>). <c>Sholto.Bench</c> is the other intended implementer:
/// a scripted surface and a scripted keyboard over the same real app.</para></summary>
public interface ISholtoEntities
{
    IControlSurface CreateControlSurface();
    IKeyboard       CreateKeyboard();
    IApplication    CreateApplication();
}
