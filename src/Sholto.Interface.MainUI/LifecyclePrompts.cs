using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels;
using Sholto.Interface.MainUI.Views;

namespace Sholto.Interface.MainUI;

/// <summary>MainUI's half of the app lifecycle: shows the existing pickers when the App asks and sends the
/// answer back as a command. <c>MusicFolderNeeded</c> opens the OS folder picker and answers
/// <c>ChooseMusicFolder</c>; <c>OutputDeviceNeeded</c> opens the device picker and answers
/// <c>ChooseOutputDevice</c>. A cancelled picker answers with a null path / name. <c>SavedThemeFound</c>
/// applies the theme the user chose last time; <c>SavedWaveformStyleFound</c> the waveform style. Call <see cref="Start"/> before the lifecycle starts, so the
/// first question finds a listener.</summary>
public sealed class LifecyclePrompts(
    IEventSubscriber subscriber,
    ICommandSender sender,
    IAppThread appThread,
    IAudioDevicePickerFactory pickerFactory,
    MainViewModel viewModel,
    Window owner) :
    IEventHandler<MusicFolderNeeded>,
    IEventHandler<OutputDeviceNeeded>,
    IEventHandler<SavedThemeFound>,
    IEventHandler<SavedWaveformStyleFound>
{
    private readonly IEventSubscriber _subscriber = subscriber;
    private readonly ICommandSender _sender = sender;
    private readonly IAppThread _appThread = appThread;
    private readonly IAudioDevicePickerFactory _pickerFactory = pickerFactory;
    private readonly MainViewModel _viewModel = viewModel;
    private readonly Window _owner = owner;

    public void Start()
    {
        _subscriber.Subscribe<MusicFolderNeeded>(this);
        _subscriber.Subscribe<OutputDeviceNeeded>(this);
        _subscriber.Subscribe<SavedThemeFound>(this);
        _subscriber.Subscribe<SavedWaveformStyleFound>(this);
    }

    public void Handle(in MusicFolderNeeded e) => _ = AskMusicFolderAsync(e.Reason);

    public void Handle(in OutputDeviceNeeded e) => _ = AskOutputDeviceAsync(e.Devices, e.CurrentName);

    /// <summary>Published on the app thread, before the App starts saving theme choices, so applying it
    /// here and now is what keeps the restore from being saved back.</summary>
    public void Handle(in SavedThemeFound e) => _viewModel.RestoreTheme(e.Name);

    /// <summary>Applies the style saved last time; a restore, so nothing is sent back.</summary>
    public void Handle(in SavedWaveformStyleFound e) => _viewModel.WaveformStyle.Restore(e.Id);

    private async Task AskMusicFolderAsync(MusicFolderReason reason)
    {
        var title = reason == MusicFolderReason.DriveNotFound
            ? "Music drive not found — reconnect it, then choose your library"
            : "Choose your music library";
        var picked = await PickMusicFolderAsync(title);
        _appThread.Post(() => _sender.Send(
            new ChooseMusicFolder(picked, new Origin(InterfaceIds.MainUI, "music-folder-picker", "choose"))));
    }

    private async Task AskOutputDeviceAsync(IReadOnlyList<OutputDeviceChoice> choices, string? currentName)
    {
        var picker = _pickerFactory.Create(choices, currentName);
        await picker.ShowDialog(_owner);
        var name = picker.SelectedDevice?.Name;
        _appThread.Post(() => _sender.Send(
            new ChooseOutputDevice(name, new Origin(InterfaceIds.MainUI, "output-device-picker", "choose"))));
    }

    /// <summary>Show the OS folder picker for the user's music library. Returns the chosen absolute path, or
    /// null if they cancelled.</summary>
    private async Task<string?> PickMusicFolderAsync(string title)
    {
        var top = TopLevel.GetTopLevel(_owner);
        if (top is null) return null;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
            });
        if (folders.Count == 0) return null;
        var uri = folders[0].Path;
        return uri.IsFile ? uri.LocalPath : uri.ToString();
    }
}
