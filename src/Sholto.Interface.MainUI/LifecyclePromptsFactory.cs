using Avalonia.Controls;
using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels;
using Sholto.Interface.MainUI.Views;

namespace Sholto.Interface.MainUI;

public sealed class LifecyclePromptsFactory(
    IEventSubscriber subscriber, ICommandSender sender, IAppThread appThread,
    IAudioDevicePickerFactory pickerFactory) : ILifecyclePromptsFactory
{
    private readonly IEventSubscriber _subscriber = subscriber;
    private readonly ICommandSender _sender = sender;
    private readonly IAppThread _appThread = appThread;
    private readonly IAudioDevicePickerFactory _pickerFactory = pickerFactory;

    public LifecyclePrompts Create(MainViewModel viewModel, Window owner) =>
        new(_subscriber, _sender, _appThread, _pickerFactory, viewModel, owner);
}
