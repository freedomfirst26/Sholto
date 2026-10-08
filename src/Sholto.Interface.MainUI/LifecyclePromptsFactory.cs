using Avalonia.Controls;
using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI;

public sealed class LifecyclePromptsFactory(
    IEventSubscriber subscriber, ICommandSender sender, IAppThread appThread) : ILifecyclePromptsFactory
{
    private readonly IEventSubscriber _subscriber = subscriber;
    private readonly ICommandSender _sender = sender;
    private readonly IAppThread _appThread = appThread;

    public LifecyclePrompts Create(MainViewModel viewModel, Window owner) =>
        new(_subscriber, _sender, _appThread, viewModel, owner);
}
