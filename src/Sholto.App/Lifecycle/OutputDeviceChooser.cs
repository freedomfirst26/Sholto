using Sholto.App.Audio;
using Sholto.App.ExternalTools;
using Sholto.Data;

namespace Sholto.App.Lifecycle;

/// <summary>See <see cref="IOutputDeviceChooser"/>: lists the output devices, picks or asks for the master
/// device, starts the audio output on it, and handles the menu's device change.</summary>
public sealed class OutputDeviceChooser(
    IAudioOutputEnumerator outputEnumerator,
    IAudioOutput audioOutput,
    IControllerSoundCard controllerSoundCard,
    ISettingPreference outputDevicePreference,
    IAppThread appThread,
    IEventPublisher publisher) : IOutputDeviceChooser
{
    private readonly IAudioOutputEnumerator _outputEnumerator = outputEnumerator;
    private readonly IAudioOutput _audioOutput = audioOutput;
    private readonly IControllerSoundCard _controllerSoundCard = controllerSoundCard;
    private readonly ISettingPreference _outputDevicePreference = outputDevicePreference;
    private readonly IAppThread _appThread = appThread;
    private readonly IEventPublisher _publisher = publisher;

    // The question currently put to the user, if any: completed by the matching Choose command.
    private TaskCompletionSource<string?>? _deviceAnswer;
    // The device master really plays on, from the last route the audio output reported (app thread only). Null until the engine starts.
    private string? _masterDevice;

    public async Task StartAudioAsync(Task beforeAsking)
    {
        // P7: the devices are listed before the database is awaited.
        var allDevices = await Task.Run(() => _outputEnumerator.EnumerateOutputs()).ConfigureAwait(false);
        if (allDevices.Count == 0)
        {
            Console.WriteLine("No audio output devices found.");
            return;
        }

        // The controller's sound card is never a pickable "master speaker" — AudioEngine auto-selects it as
        // the device to open (4ch: master 1-2 + headphone cue 3-4) whenever it's connected, and the picker
        // only offers where to re-route master to. See AudioEngine.Start for the full story.
        bool controllerCardPresent = allDevices.Any(d => _controllerSoundCard.Matches(d.Name));
        var devices = allDevices.Where(d => !_controllerSoundCard.Matches(d.Name)).ToList();

        var savedName = await _outputDevicePreference.GetAsync().ConfigureAwait(false);
        var chosen = devices.FirstOrDefault(d => d.Name == savedName);

        if (chosen is null && devices.Count > 0)
        {
            // Not while the Folders panel is open: wait for its question to be settled.
            await beforeAsking.ConfigureAwait(false);
            chosen = await RequestDeviceAsync(devices, savedName).ConfigureAwait(false);
        }

        // No speaker chosen (cancelled/none): fine if the controller is here to fall back on (master+cue
        // both play from it, like before this feature). Otherwise there's genuinely nothing to play through.
        if (chosen is null && !controllerCardPresent) return;

        if (chosen is not null)
            await _outputDevicePreference.SetAsync(chosen.Name).ConfigureAwait(false);

        var route = await _audioOutput.StartAsync(chosen?.Name).ConfigureAwait(false);
        await _appThread.InvokeAsync(() => _masterDevice = route.DeviceName).ConfigureAwait(false);
    }

    /// <summary>Menu entry point: ask which device master should play through and switch to it. Nothing
    /// happens if the user cancels or picks the device already in use. If the controller's card is the only
    /// device connected there is nothing to offer; master stays on it.</summary>
    public void Handle(in ChangeOutputDevice command) => _ = ChangeOutputDeviceAsync();

    private async Task ChangeOutputDeviceAsync()
    {
        if (_deviceAnswer is not null) return;  // already asking

        // Excludes the controller's sound card — see StartAudioAsync's comment.
        var allDevices = await Task.Run(() => _outputEnumerator.EnumerateOutputs()).ConfigureAwait(false);
        var devices = allDevices.Where(d => !_controllerSoundCard.Matches(d.Name)).ToList();
        if (devices.Count == 0) return;

        // Where master really plays (not the saved wish), if it is one of the devices on offer. A failed route
        // leaves it on the controller, which is not in the list: then nothing reads as in use and the speakers
        // can be picked again.
        var masterDevice = await _appThread.InvokeAsync(() => _masterDevice).ConfigureAwait(false);
        var currentName = devices.Any(d => d.Name == masterDevice) ? masterDevice : null;
        var chosen = await RequestDeviceAsync(devices, currentName).ConfigureAwait(false);
        if (chosen is null || chosen.Name == currentName) return;

        await _outputDevicePreference.SetAsync(chosen.Name).ConfigureAwait(false);

        var route = await _audioOutput.SwitchAsync(chosen.Name).ConfigureAwait(false);
        await _appThread.InvokeAsync(() => _masterDevice = route.DeviceName).ConfigureAwait(false);
    }

    public void Handle(in ChooseOutputDevice command)
    {
        var answer = _deviceAnswer;
        if (answer is null) return;
        _deviceAnswer = null;
        answer.TrySetResult(command.Name);
    }

    private async Task<AudioDevice?> RequestDeviceAsync(IReadOnlyList<AudioDevice> devices, string? currentName)
    {
        var choices = devices.Select(d => new OutputDeviceChoice(d.Name, d.IsDefault)).ToList();
        var answer = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await _appThread.InvokeAsync(() =>
        {
            _deviceAnswer = answer;
            _publisher.Publish(new OutputDeviceNeeded(choices, currentName));
        });
        var name = await answer.Task.ConfigureAwait(false);
        return name is null ? null : devices.FirstOrDefault(d => d.Name == name);
    }
}
