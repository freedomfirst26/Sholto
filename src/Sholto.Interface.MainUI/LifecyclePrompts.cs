using System.ComponentModel;
using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels;

namespace Sholto.Interface.MainUI;

/// <summary>MainUI's half of the app lifecycle: shows the existing pickers when the App asks and sends the
/// answer back as a command. <c>FoldersNeeded</c> opens the Folders panel, which sends <c>ChooseFolders</c>
/// itself when the user continues or dismisses it; <c>OutputDeviceNeeded</c> shows the in-window device picker
/// and answers <c>ChooseOutputDevice</c> (a null name when cancelled). The two are never open together: a
/// question arriving while the other one is open waits until it closes. <c>SavedThemeFound</c> applies the
/// theme the user chose last time; <c>SavedWaveformStyleFound</c> the waveform style. Call <see cref="Start"/>
/// before the lifecycle starts, so the first question finds a listener.</summary>
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

    // A question that arrived while the other one was open: shown once that one closes. Only the latest counts.
    private FoldersNeeded? _waitingFolders;
    private OutputDeviceNeeded? _waitingDevice;

    public void Start()
    {
        _viewModel.PropertyChanged += OnViewModelChanged;
        _viewModel.Folders.PropertyChanged += OnFoldersChanged;
        _subscriber.Subscribe<FoldersNeeded>(this);
        _subscriber.Subscribe<OutputDeviceNeeded>(this);
        _subscriber.Subscribe<SavedThemeFound>(this);
        _subscriber.Subscribe<SavedWaveformStyleFound>(this);
    }

    /// <summary>Opens the Folders panel, unless the output picker is open: then it waits until the picker closes.</summary>
    public void Handle(in FoldersNeeded e)
    {
        if (_viewModel.IsOutputPickerOpen)
        {
            _waitingFolders = e;
            return;
        }
        _viewModel.Folders.Show(e);
    }

    /// <summary>Opens the output picker, unless the Folders panel is open: then it waits until the panel closes.</summary>
    public void Handle(in OutputDeviceNeeded e)
    {
        if (_viewModel.Folders.IsOpen)
        {
            _waitingDevice = e;
            return;
        }
        _ = AskOutputDeviceAsync(e.Devices, e.CurrentName);
    }

    /// <summary>Published on the app thread, before the App starts saving theme choices, so applying it
    /// here and now is what keeps the restore from being saved back.</summary>
    public void Handle(in SavedThemeFound e) => _viewModel.RestoreTheme(e.Name);

    /// <summary>Applies the style saved last time; a restore, so nothing is sent back.</summary>
    public void Handle(in SavedWaveformStyleFound e) => _viewModel.WaveformStyle.Restore(e.Id);

    // The output picker closed: a waiting folders question opens now. Posted, so it opens after the close has
    // finished rather than inside its change notification; asked again there, in case the picker reopened.
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.IsOutputPickerOpen) || _viewModel.IsOutputPickerOpen) return;
        if (_waitingFolders is not { } question) return;
        _waitingFolders = null;
        _appThread.Post(() => Handle(question));
    }

    // The Folders panel closed: a waiting device question opens now (see OnViewModelChanged).
    private void OnFoldersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IFoldersViewModel.IsOpen) || _viewModel.Folders.IsOpen) return;
        if (_waitingDevice is not { } question) return;
        _waitingDevice = null;
        _appThread.Post(() => Handle(question));
    }

    private async Task AskOutputDeviceAsync(IReadOnlyList<OutputDeviceChoice> choices, string? currentName)
    {
        try
        {
            // The answer goes back through the app thread, so the continuation need not resume on any context.
            var name = await _viewModel.OutputPicker.AskAsync(choices, currentName).ConfigureAwait(false);
            _appThread.Post(() => _sender.Send(
                new ChooseOutputDevice(name, new Origin(InterfaceIds.MainUI, "output-device-picker", "choose"))));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OutputDevice] could not ask for the output device: {ex.Message}");
        }
    }
}
