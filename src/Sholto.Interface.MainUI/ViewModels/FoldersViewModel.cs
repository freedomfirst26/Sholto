using System.ComponentModel;
using System.Globalization;
using Avalonia.Input;
using Sholto.Data;
using Sholto.Interface.MainUI.Controls.Modal;

namespace Sholto.Interface.MainUI.ViewModels;

/// <inheritdoc />
/// <remarks>Follows <see cref="AnalysisFolderChanged"/> (the panel closes) and <see cref="AnalysisFolderMoveProgress"/>
/// (the status line) itself, as <see cref="SettingsViewModel"/> follows its state events, and asks the App
/// <see cref="CheckFolders"/> through <see cref="IQueryAsker"/> after every pick; only the latest answer counts.</remarks>
public sealed class FoldersViewModel : IFoldersViewModel, IEventHandler<AnalysisFolderChanged>,
    IEventHandler<AnalysisFolderMoveProgress>
{
    private const long ApproxStemBytesPerTrack = 175L * 1024 * 1024;

    private readonly ICommandSender _sender;
    private readonly IQueryAsker _asker;
    private readonly List<FolderRowViewModel> _rows;
    private FoldersReason _reason;
    private bool _isOpen;
    private bool _isWorking;
    private string? _progress;
    private bool _analysisPicked;
    private int _checkSeq;
    private bool _checkPending;
    private long _stemBytes;
    private string? _stemsAt;

    public FoldersViewModel(IEventSubscriber subscriber, ICommandSender sender, IQueryAsker asker)
    {
        _sender = sender;
        _asker = asker;
        Music = new FolderRowViewModel("Music folder", "Set by SHOLTO_MUSIC_DIR");
        Analysis = new FolderRowViewModel("Analysis folder", "Set by SHOLTO_CACHE_ROOT");
        _rows = [Music, Analysis];
        subscriber.Subscribe<AnalysisFolderChanged>(this);
        subscriber.Subscribe<AnalysisFolderMoveProgress>(this);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public FolderRowViewModel Music { get; }

    public FolderRowViewModel Analysis { get; }

    public IReadOnlyList<FolderRowViewModel> Rows => _rows;

    public bool IsOpen
    {
        get => _isOpen;
        private set
        {
            if (_isOpen == value) return;
            _isOpen = value;
            Notify(nameof(IsOpen));
        }
    }

    public bool IsWorking
    {
        get => _isWorking;
        private set
        {
            if (_isWorking == value) return;
            _isWorking = value;
            Music.SetBusy(value);
            Analysis.SetBusy(value);
            Notify(nameof(IsWorking));
            Notify(nameof(StatusText));
            Notify(nameof(CanConfirm));
        }
    }

    public string StatusText => _progress ?? "Applying…";

    public string Eyebrow => "FOLDERS";

    public ModalTone Tone => _reason == FoldersReason.DriveNotFound ? ModalTone.Attention : ModalTone.Accent;

    public string Title => _reason switch
    {
        FoldersReason.FirstRun => "Where are your music and analysis folders?",
        FoldersReason.Upgrade => "Choose where Sholto keeps its analysis",
        FoldersReason.DriveNotFound => "A folder can't be reached — reconnect the drive or choose another",
        _ => "Folders",
    };

    public string? Subtitle => _reason switch
    {
        FoldersReason.Upgrade =>
            $"Stems are in {_stemsAt ?? "the default folder"} ({FormatSize(_stemBytes)}). Choosing another folder moves them.",
        FoldersReason.Change => "Changing the analysis folder moves the stems already analysed.",
        _ => "The analysis folder holds the stems Sholto separates from your tracks. They can be regenerated.",
    };

    public string KeyHint => _reason == FoldersReason.Change
        ? "Enter continue · Esc cancel"
        : "Enter continue · Esc not now";

    public ModalWidth Width => ModalWidth.Regular;

    public ModalButtons Buttons => new(_reason == FoldersReason.Change ? "Cancel" : "Not now", null, "Continue");

    public ModalScrimClick ScrimClick =>
        _reason == FoldersReason.Change ? ModalScrimClick.Dismisses : ModalScrimClick.Ignored;

    public bool CapturesText => false;

    public bool CanGoBack => false;

    public bool CanConfirm => _isOpen && !_isWorking && !_checkPending && Music.IsValid && Analysis.IsValid;

    public void Show(FoldersNeeded question)
    {
        _checkSeq++;
        _checkPending = false;
        _progress = null;
        _reason = question.Reason;
        _stemBytes = question.AnalysisStemBytes;
        _stemsAt = question.Analysis.Path;
        Music.Load(question.Music);
        Analysis.Load(question.Analysis);
        // A folder the user (or an upgrade) already has stands; only a suggestion follows the music folder.
        _analysisPicked = question.Analysis.State != FolderRowState.NotChosen || question.Reason == FoldersReason.Upgrade;
        Analysis.ShowDetail(DescribeStems());
        IsWorking = false;
        IsOpen = true;
        NotifyContent();
    }

    public void Pick(FolderRowViewModel row, string path)
    {
        if (!_isOpen || _isWorking || row.IsFixed) return;
        row.Set(path);
        if (ReferenceEquals(row, Analysis)) _analysisPicked = true;
        _ = CheckAsync(++_checkSeq);
    }

    public void Confirm()
    {
        if (!CanConfirm) return;
        IsWorking = true;
        _sender.Send(new ChooseFolders(Music.Path, Analysis.Path,
            new Origin(InterfaceIds.MainUI, "folders-panel", "continue")));
    }

    /// <summary>Esc, a scrim press (on Change) and the Dismiss button. Answers "no change" so the App stops
    /// waiting. Does nothing while the App is applying an answer, a move included.</summary>
    public void Dismiss()
    {
        if (!_isOpen || _isWorking) return;
        _checkSeq++;
        _checkPending = false;
        IsOpen = false;
        _sender.Send(new ChooseFolders(null, null, new Origin(InterfaceIds.MainUI, "folders-panel", "dismiss")));
    }

    public void Back() { }

    public bool HandleKey(Key key, KeyModifiers modifiers) => false;

    public void Handle(in AnalysisFolderChanged e)
    {
        if (!_isOpen) return;
        _checkSeq++;
        _checkPending = false;
        _progress = null;
        IsWorking = false;
        IsOpen = false;
    }

    public void Handle(in AnalysisFolderMoveProgress e)
    {
        if (!_isOpen) return;
        _progress = $"Moving stems: {e.Done} of {e.Total} track folders";
        Notify(nameof(StatusText));
    }

    private async Task CheckAsync(int seq)
    {
        _checkPending = true;
        Notify(nameof(CanConfirm));
        while (true)
        {
            var asked = new CheckFolders(Music.Path, Analysis.Path);
            var task = _asker.Ask<CheckFolders, Task<FolderCheckResult>>(asked);
            var result = task is null ? default : await task;
            if (seq != _checkSeq) return;

            Music.ShowProblem(result.MusicProblem);
            // The default follows the music folder until the user picks the analysis folder; the new default is
            // checked in turn.
            if (!_analysisPicked && result.SuggestedAnalysis is { } suggested && suggested != Analysis.Path)
            {
                Analysis.Set(suggested);
                continue;
            }
            Analysis.ShowProblem(result.AnalysisProblem);
            break;
        }
        _checkPending = false;
        Notify(nameof(CanConfirm));
    }

    private string? DescribeStems() =>
        _stemBytes > 0
            ? $"{FormatSize(_stemBytes)} of stems here · about {FormatSize(ApproxStemBytesPerTrack)} per track"
            : $"Stems take about {FormatSize(ApproxStemBytesPerTrack)} per track";

    private string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0
            ? $"{bytes} B"
            : $"{value.ToString(value >= 100 ? "0" : "0.0", CultureInfo.InvariantCulture)} {units[unit]}";
    }

    private void NotifyContent()
    {
        Notify(nameof(Tone));
        Notify(nameof(Title));
        Notify(nameof(Subtitle));
        Notify(nameof(KeyHint));
        Notify(nameof(Buttons));
        Notify(nameof(ScrimClick));
        Notify(nameof(CanConfirm));
        Notify(nameof(StatusText));
    }

    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
