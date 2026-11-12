using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI;

public sealed class LifecyclePromptsFactory(
    IEventSubscriber subscriber, ICommandSender sender, IAppThread appThread, ILogFactory logs) : ILifecyclePromptsFactory
{
    private readonly IEventSubscriber _subscriber = subscriber;
    private readonly ICommandSender _sender = sender;
    private readonly IAppThread _appThread = appThread;
    private readonly ILogFactory _logs = logs;

    public LifecyclePrompts Create(MainViewModel viewModel) =>
        new(_subscriber, _sender, _appThread, viewModel, _logs);
}
