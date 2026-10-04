using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

public sealed class OverlayViewModelFactory(
    ITagRecency tagRecency,
    IQueryAsker asker,
    ICommandSender sender,
    IEventSubscriber subscriber,
    IAppThread appThread) : IOverlayViewModelFactory
{
    private readonly ITagRecency _tagRecency = tagRecency;
    private readonly IQueryAsker _asker = asker;
    private readonly ICommandSender _sender = sender;
    private readonly IEventSubscriber _subscriber = subscriber;
    private readonly IAppThread _appThread = appThread;

    public TagEditorViewModel TagEditor() => new(_asker, _sender, _subscriber, _tagRecency);

    public CratePickerViewModel CratePicker() => new(_asker, _sender, _appThread);
}
