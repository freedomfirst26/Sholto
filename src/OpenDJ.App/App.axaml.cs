using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using OpenDJ.Audio;
using OpenDJ.Controller;
using OpenDJ.Library;
using OpenDJ.App.ViewModels;

namespace OpenDJ.App;

public partial class App : Application
{
    private AudioEngine? _audioEngine;
    private MidiManager? _midi;
    private DispatcherTimer? _positionTimer;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainViewModel();

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

            // Start audio engine
            _audioEngine = new AudioEngine(vm.DeckA.Player);
            try { _audioEngine.Start(); }
            catch (Exception ex)
            {
                Console.WriteLine($"Audio engine failed to start: {ex.Message}");
            }

            // Connect MIDI
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
                    }
                });
            };

            // 60fps position sync
            _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _positionTimer.Tick += (_, _) => vm.DeckA.SyncPlayPosition();
            _positionTimer.Start();

            desktop.MainWindow = new Views.MainWindow { DataContext = vm };
            desktop.Exit += (_, _) =>
            {
                _positionTimer?.Stop();
                _audioEngine?.Stop();
                _midi?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
