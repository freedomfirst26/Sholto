using Sholto.App;
using Sholto.App.Lifecycle;
using Sholto.Interface.MainUI.Theming;
using Sholto.Data;

using Sholto.Interface.MainUI;

namespace Sholto.Host;

/// <summary>
/// The composed object graph <see cref="App"/> is handed: entities, the pre-built
/// plain-.NET leaves, options, and the factories App uses once Avalonia is up.
/// </summary>
public sealed class AppStack(
    ISholtoEntities entities, SholtoStack stack, SholtoOptions options,
    ICoreFactory coreFactory, IAppLifecycleFactory lifecycleFactory, ILifecyclePromptsFactory promptsFactory,
    IThemeStackFactory themeStackFactory, IControllerStackFactory controllerStackFactory,
    IInputStackFactory inputStackFactory, IMasterCueOutput masterCueOutput,
    IFrameClock frameClock, IAppThread appThread, ICommandSender sender, ITrayFactory trayFactory)
{
    public ISholtoEntities Entities { get; } = entities;
    public SholtoStack Stack { get; } = stack;
    public SholtoOptions Options { get; } = options;
    public ICoreFactory CoreFactory { get; } = coreFactory;
    public IAppLifecycleFactory LifecycleFactory { get; } = lifecycleFactory;
    public ILifecyclePromptsFactory PromptsFactory { get; } = promptsFactory;
    public IThemeStackFactory ThemeStackFactory { get; } = themeStackFactory;
    public IControllerStackFactory ControllerStackFactory { get; } = controllerStackFactory;
    public IInputStackFactory InputStackFactory { get; } = inputStackFactory;
    public IMasterCueOutput MasterCueOutput { get; } = masterCueOutput;
    public IFrameClock FrameClock { get; } = frameClock;
    public IAppThread AppThread { get; } = appThread;
    /// <summary>The bus's command side, for the root's own reports (the controller's connection state).</summary>
    public ICommandSender Sender { get; } = sender;
    public ITrayFactory TrayFactory { get; } = trayFactory;
}
