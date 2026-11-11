using Sholto.App.Performance;
using Sholto.Data;
using System.Globalization;

namespace Sholto.App.Lifecycle;

/// <summary>See <see cref="ISettingsRestorer"/>: restores and saves the theme, waveform style and backspin
/// time and distance. Each restore waits for the database to open (null means no restore and no persistence);
/// a backspin choice made before then is held and saved once it does, and wins over the saved value.</summary>
public sealed class SettingsRestorer(
    ILibraryDatabase database,
    ISettingPreference themePreference,
    ISettingPreference waveformStylePreference,
    ISettingPreference backspinTimePreference,
    ISettingPreference backspinDistancePreference,
    IBackspinFeel backspinFeel,
    IAppThread appThread,
    IEventPublisher publisher) : ISettingsRestorer
{
    private readonly ILibraryDatabase _database = database;
    private readonly ISettingPreference _themePreference = themePreference;
    private readonly ISettingPreference _waveformStylePreference = waveformStylePreference;
    private readonly ISettingPreference _backspinTimePreference = backspinTimePreference;
    private readonly ISettingPreference _backspinDistancePreference = backspinDistancePreference;
    private readonly IBackspinFeel _backspinFeel = backspinFeel;
    private readonly IAppThread _appThread = appThread;
    private readonly IEventPublisher _publisher = publisher;

    // True once the saved theme was restored on a database that opened: from then on a chosen theme is saved.
    private bool _themePersistence;
    // Same rule for the waveform style.
    private bool _waveformStylePersistence;
    // And for the backspin time and distance. A choice made before then is held (not dropped) and saved
    // when persistence arms; guarded by _backspinGate because the choice and the restore are on different threads.
    private readonly object _backspinGate = new();
    private bool _backspinTimePersistence;
    private double? _pendingBackspinTime;
    private bool _backspinDistancePersistence;
    private double? _pendingBackspinDistance;

    public void Start()
    {
        // Restore the previously-selected theme. Runs on its own task so it doesn't block the music-dir
        // resolution.
        _ = Task.Run(RestoreThemeAsync);

        // Restore the previously-selected waveform style, the same way.
        _ = Task.Run(RestoreWaveformStyleAsync);

        // Restore the backspin time and distance, the same way; a choice is saved once that is done.
        _backspinFeel.TimeChosen += OnBackspinTimeChosen;
        _backspinFeel.DistanceChosen += OnBackspinDistanceChosen;
        _ = Task.Run(RestoreBackspinTimeAsync);
        _ = Task.Run(RestoreBackspinDistanceAsync);
    }

    private async Task RestoreThemeAsync()
    {
        var database = await _database.Opened;
        if (database is null) return;

        var savedName = await _themePreference.GetAsync();
        if (!string.IsNullOrEmpty(savedName))
            await _appThread.InvokeAsync(() => _publisher.Publish(new SavedThemeFound(savedName)));

        _themePersistence = true;
    }

    public void Handle(in ChooseTheme command)
    {
        if (!_themePersistence) return;
        var name = command.Name;
        _ = Task.Run(async () =>
        {
            try
            {
                await _themePreference.SetAsync(name);
            }
            catch (Exception ex) { Console.WriteLine($"[Theme] persist failed: {ex.Message}"); }
        });
    }

    // ---- Waveform style ------------------------------------------------------------------------

    private async Task RestoreWaveformStyleAsync()
    {
        var database = await _database.Opened;
        if (database is null) return;

        var savedId = await _waveformStylePreference.GetAsync();
        if (!string.IsNullOrEmpty(savedId))
            await _appThread.InvokeAsync(() => _publisher.Publish(new SavedWaveformStyleFound(savedId)));

        _waveformStylePersistence = true;
    }

    public void Handle(in ChooseWaveformStyle command)
    {
        if (!_waveformStylePersistence) return;
        var id = command.Id;
        _ = Task.Run(async () =>
        {
            try
            {
                await _waveformStylePreference.SetAsync(id);
            }
            catch (Exception ex) { Console.WriteLine($"[WaveformStyle] persist failed: {ex.Message}"); }
        });
    }

    // ---- Backspin time and distance -------------------------------------------------------------

    private async Task RestoreBackspinTimeAsync()
    {
        var database = await _database.Opened;
        if (database is null) return;

        var saved = await _backspinTimePreference.GetAsync();
        if (double.TryParse(saved, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            await _appThread.InvokeAsync(() =>
            {
                // A choice made while the database was opening wins over the saved value.
                lock (_backspinGate) { if (_pendingBackspinTime is not null) return; }
                _backspinFeel.RestoreSeconds(seconds);
            });

        double? pending;
        lock (_backspinGate)
        {
            _backspinTimePersistence = true;
            pending = _pendingBackspinTime;
            _pendingBackspinTime = null;
        }
        if (pending is { } value) SaveBackspin(_backspinTimePreference, value, "BackspinTime");
    }

    private void OnBackspinTimeChosen(double seconds)
    {
        lock (_backspinGate)
        {
            if (!_backspinTimePersistence)
            {
                _pendingBackspinTime = seconds;
                return;
            }
        }
        SaveBackspin(_backspinTimePreference, seconds, "BackspinTime");
    }

    private async Task RestoreBackspinDistanceAsync()
    {
        var database = await _database.Opened;
        if (database is null) return;

        var saved = await _backspinDistancePreference.GetAsync();
        if (double.TryParse(saved, NumberStyles.Float, CultureInfo.InvariantCulture, out var beats))
            await _appThread.InvokeAsync(() =>
            {
                lock (_backspinGate) { if (_pendingBackspinDistance is not null) return; }
                _backspinFeel.RestoreBeats(beats);
            });

        double? pending;
        lock (_backspinGate)
        {
            _backspinDistancePersistence = true;
            pending = _pendingBackspinDistance;
            _pendingBackspinDistance = null;
        }
        if (pending is { } value) SaveBackspin(_backspinDistancePreference, value, "BackspinDistance");
    }

    private void OnBackspinDistanceChosen(double beats)
    {
        lock (_backspinGate)
        {
            if (!_backspinDistancePersistence)
            {
                _pendingBackspinDistance = beats;
                return;
            }
        }
        SaveBackspin(_backspinDistancePreference, beats, "BackspinDistance");
    }

    private void SaveBackspin(ISettingPreference preference, double value, string tag)
    {
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        _ = Task.Run(async () =>
        {
            try
            {
                await preference.SetAsync(text);
            }
            catch (Exception ex) { Console.WriteLine($"[{tag}] persist failed: {ex.Message}"); }
        });
    }
}
