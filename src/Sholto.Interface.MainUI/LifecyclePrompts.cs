using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI;

/// <summary>MainUI's half of the app lifecycle: shows the existing pickers when the App asks and sends the
/// answer back as a command. <c>FoldersNeeded</c> opens the Folders panel, which sends
/// <c>ChooseFolders</c> itself when the user continues or dismisses it; <c>OutputDeviceNeeded</c> shows the in-window device picker and answers
/// <c>ChooseOutputDevice</c>. A cancelled picker answers with a null name. <c>SavedThemeFound</c>
/// applies the theme the user chose last time; <c>SavedWaveformStyleFound</c> the waveform style. Call <see cref="Start"/> before the lifecycle starts, so the
/// first question finds a listener.</summary>
public sealed class LifecyclePrompts(
    IEventSubscriber subscriber,
    ICommandSender sender,
    IAppThread appThread,
    MainViewModel viewModel) :
    IEventHandler<FoldersNeeded>,
    IEventHandler<OutputDeviceNeeded>,
    IEventHandler<SavedThemeFound>,
    IEventHandler<SavedWaveformStyleFound>
{
    private readonly IEventSubscriber _subscriber = subscriber;
    private readonly ICommandSender _sender = sender;
    private readonly IAppThread _appThread = appThread;
    private readonly MainViewModel _viewModel = viewModel;

    public void Start()
    {
        _subscriber.Subscribe<FoldersNeeded>(this);
        _subscriber.Subscribe<OutputDeviceNeeded>(this);
        _subscriber.Subscribe<SavedThemeFound>(this);
        _subscriber.Subscribe<SavedWaveformStyleFound>(this);
    }

    public void Handle(in FoldersNeeded e) => _viewModel.Folders.Show(e);

    public void Handle(in OutputDeviceNeeded e) => _ = AskOutputDeviceAsync(e.Devices, e.CurrentName);

    /// <summary>Published on the app thread, before the App starts saving theme choices, so applying it
    /// here and now is what keeps the restore from being saved back.</summary>
    public void Handle(in SavedThemeFound e) => _viewModel.RestoreTheme(e.Name);

    /// <summary>Applies the style saved last time; a restore, so nothing is sent back.</summary>
    public void Handle(in SavedWaveformStyleFound e) => _viewModel.WaveformStyle.Restore(e.Id);

    private async Task AskOutputDeviceAsync(IReadOnlyList<OutputDeviceChoice> choices, string? currentName)
    {
        var name = await _viewModel.OutputPicker.AskAsync(choices, currentName);
        _appThread.Post(() => _sender.Send(
            new ChooseOutputDevice(name, new Origin(InterfaceIds.MainUI, "output-device-picker", "choose"))));
    }
}
