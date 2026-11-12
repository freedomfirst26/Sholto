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

    public void Start()
    {
        _subscriber.Subscribe<FoldersNeeded>(this);
        _subscriber.Subscribe<OutputDeviceNeeded>(this);
        _subscriber.Subscribe<SavedThemeFound>(this);
        _subscriber.Subscribe<SavedWaveformStyleFound>(this);
    }

    public void Handle(in FoldersNeeded e) => _ = ShowFoldersAsync(e);

    public void Handle(in OutputDeviceNeeded e) => _ = AskOutputDeviceAsync(e.Devices, e.CurrentName);

    /// <summary>Published on the app thread, before the App starts saving theme choices, so applying it
    /// here and now is what keeps the restore from being saved back.</summary>
    public void Handle(in SavedThemeFound e) => _viewModel.RestoreTheme(e.Name);

    /// <summary>Applies the style saved last time; a restore, so nothing is sent back.</summary>
    public void Handle(in SavedWaveformStyleFound e) => _viewModel.WaveformStyle.Restore(e.Id);

    private async Task ShowFoldersAsync(FoldersNeeded question)
    {
        try
        {
            // Not over the output picker: the panel opens once the picker has closed.
            while (_viewModel.IsOutputPickerOpen)
                await ClosedAsync(_viewModel, nameof(MainViewModel.IsOutputPickerOpen), () => _viewModel.IsOutputPickerOpen);
            _viewModel.Folders.Show(question);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Folders] could not show the Folders panel: {ex.Message}");
        }
    }

    private async Task AskOutputDeviceAsync(IReadOnlyList<OutputDeviceChoice> choices, string? currentName)
    {
        try
        {
            // Not over the Folders panel: the picker opens once the panel has closed.
            while (_viewModel.Folders.IsOpen)
                await ClosedAsync(_viewModel.Folders, nameof(IFoldersViewModel.IsOpen), () => _viewModel.Folders.IsOpen);
            var name = await _viewModel.OutputPicker.AskAsync(choices, currentName);
            _appThread.Post(() => _sender.Send(
                new ChooseOutputDevice(name, new Origin(InterfaceIds.MainUI, "output-device-picker", "choose"))));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OutputDevice] could not ask for the output device: {ex.Message}");
        }
    }

    /// <summary>Completes on the app thread after <paramref name="property"/> of <paramref name="source"/> next
    /// changes with <paramref name="isOpen"/> false: posted, so the other question opens after the close has
    /// finished rather than inside its change notification.</summary>
    private Task ClosedAsync(INotifyPropertyChanged source, string property, Func<bool> isOpen)
    {
        var closed = new TaskCompletionSource();
        PropertyChangedEventHandler? onChanged = null;
        onChanged = (_, e) =>
        {
            if (e.PropertyName != property || isOpen()) return;
            source.PropertyChanged -= onChanged;
            _appThread.Post(() => closed.TrySetResult());
        };
        source.PropertyChanged += onChanged;
        return closed.Task;
    }
}
