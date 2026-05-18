using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using OpenDJ.Audio;
using OpenDJ.Controller;
using OpenDJ.Library;
using OpenDJ.App.ViewModels;
using OpenDJ.App.Views;

namespace OpenDJ.App;

public partial class App : Application
{
    private AudioEngine? _audioEngine;
    private MidiManager? _midi;
    private DispatcherTimer? _positionTimer;
    private MainViewModel? _vm;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainViewModel();
            _vm = vm;
            desktop.MainWindow = new Views.MainWindow { DataContext = vm };

            // Let the window paint its first frame, THEN initialize services.
            // Posting at Background priority ensures Render runs before InitializeServices.
            desktop.MainWindow.Opened += (_, _) =>
                Dispatcher.UIThread.Post(() => InitializeServices(vm, desktop),
                    DispatcherPriority.Background);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeServices(MainViewModel vm, IClassicDesktopStyleApplicationLifetime desktop)
    {
        // Scan ~/Music in background
        var musicDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Music");
        _ = Task.Run(async () =>
        {
            var tracks = await TrackScanner.ScanAsync(musicDir);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var t in tracks) vm.Tracks.Add(t);
                if (vm.Tracks.Count > 0) vm.SelectTrack(0);
            });
        });

        // Pick audio output device (prompt user on first run or if saved device is gone)
        _ = StartAudioAsync(vm, desktop);

        _midi = new MidiManager();
        if (!_midi.Connect())
            Console.WriteLine("DDJ-FLX4 not found — use UI controls.");

        _midi.EventReceived += evt =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                switch (evt)
                {
                    case ControllerEvent.BrowseRotated r:
                        vm.OnBrowseRotated(r.Delta);
                        break;
                    case ControllerEvent.BrowsePressed:
                        vm.OnBrowsePressed(t => AudioFileDecoder.Decode(t.FilePath));
                        break;
                    case ControllerEvent.PlayPressed p:
                        vm.OnPlayPressed(p.Deck);
                        break;
                    case ControllerEvent.JogRotated j:
                        // ~50 ms per jog tick — comfortable scrub speed.
                        if (j.Deck == 0) vm.DeckA.Player.SeekRelative(j.Delta * 0.05);
                        break;
                }
            });
        };

        // Position sync only fires when the deck is actually playing
        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _positionTimer.Tick += (_, _) =>
        {
            if (vm.DeckA.Player.IsPlaying) vm.DeckA.SyncPlayPosition();
        };
        _positionTimer.Start();

        desktop.Exit += (_, _) =>
        {
            _positionTimer?.Stop();
            _audioEngine?.Stop();
            _midi?.Dispose();
        };
    }

    private async Task StartAudioAsync(MainViewModel vm, IClassicDesktopStyleApplicationLifetime desktop)
    {
        var devices = await Task.Run(() => AudioDevices.EnumerateOutputs());
        if (devices.Count == 0)
        {
            Console.WriteLine("No audio output devices found.");
            return;
        }

        var config = AppConfig.Load();
        var chosen = devices.FirstOrDefault(d => d.Name == config.OutputDeviceName);

        if (chosen is null)
            chosen = await PromptForDeviceAsync(devices, config.OutputDeviceName, desktop.MainWindow!);

        if (chosen is null) return;

        config.OutputDeviceName = chosen.Name;
        config.Save();

        await Task.Run(() =>
        {
            try
            {
                var engine = new AudioEngine(vm.DeckA.Player);
                engine.Start(chosen.Name);
                _audioEngine = engine;
                Console.WriteLine($"Audio engine started on: {chosen.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Audio engine failed to start: {ex.Message}");
            }
        });
    }

    public async Task<AudioDevice?> PromptForDeviceAsync(
        IReadOnlyList<AudioDevice> devices, string? currentName, Avalonia.Controls.Window owner)
    {
        var picker = new AudioDevicePicker(devices, currentName);
        await picker.ShowDialog(owner);
        return picker.SelectedDevice;
    }

    public async Task ChangeOutputDeviceAsync(Avalonia.Controls.Window owner)
    {
        if (_vm is null) return;

        var devices = await Task.Run(() => AudioDevices.EnumerateOutputs());
        if (devices.Count == 0) return;

        var config = AppConfig.Load();
        var chosen = await PromptForDeviceAsync(devices, config.OutputDeviceName, owner);
        if (chosen is null || chosen.Name == config.OutputDeviceName) return;

        config.OutputDeviceName = chosen.Name;
        config.Save();

        // Use SoundFlow's runtime device switch (preserves the audio graph).
        await Task.Run(() =>
        {
            try
            {
                if (_audioEngine is null)
                {
                    var engine = new AudioEngine(_vm.DeckA.Player);
                    engine.Start(chosen.Name);
                    _audioEngine = engine;
                }
                else
                {
                    _audioEngine.SwitchDevice(chosen.Name);
                }
                Console.WriteLine($"Audio engine switched to: {chosen.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Audio engine failed to start: {ex.Message}");
            }
        });
    }
}
