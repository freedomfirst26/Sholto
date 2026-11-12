using System.ComponentModel;
using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>One row of the Folders panel (Music or Analysis): its label, path, the problem or detail line and
/// whether its Change… button works. <see cref="FoldersViewModel"/> owns and updates it.</summary>
public sealed class FolderRowViewModel(string label, string fixedNote) : INotifyPropertyChanged
{
    private readonly string _fixedNote = fixedNote;
    private string? _path;
    private string? _problem;
    private string? _detail;
    private FolderRowState _state = FolderRowState.NotChosen;
    private bool _isBusy;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Label { get; } = label;

    /// <summary>The folder, or null while none is chosen.</summary>
    public string? Path => _path;

    /// <summary>What the path line shows: the path, or a prompt while none is chosen.</summary>
    public string PathText => _path ?? "No folder chosen";

    /// <summary>Why the folder cannot be used, worded for the user; null when it is fine.</summary>
    public string? Problem => _problem;

    public bool HasProblem => _problem is not null;

    /// <summary>A neutral line under the path (the stem size on the analysis row); null for none.</summary>
    public string? Detail => _detail;

    public bool HasDetail => _detail is not null && _problem is null;

    public FolderRowState State => _state;

    /// <summary>An environment variable sets this folder, so the user cannot change it.</summary>
    public bool IsFixed => _state == FolderRowState.Fixed;

    public bool CanChange => !IsFixed && !_isBusy;

    public string ChangeLabel => IsFixed ? _fixedNote : "Change…";

    /// <summary>The path is set and acceptable.</summary>
    public bool IsValid => _path is not null && _problem is null && _state != FolderRowState.Missing;

    public void Load(FolderRow row)
    {
        _path = row.Path;
        _state = row.State;
        _problem = row.Problem ?? (row.State == FolderRowState.Missing ? "This folder can't be reached right now." : null);
        NotifyAll();
    }

    /// <summary>The user picked a folder (or the panel took a suggestion): it stands until the check says otherwise.</summary>
    public void Set(string path)
    {
        _path = path;
        _state = FolderRowState.Ok;
        _problem = null;
        NotifyAll();
    }

    public void ShowProblem(string? problem)
    {
        _problem = problem;
        NotifyAll();
    }

    public void ShowDetail(string? detail)
    {
        _detail = detail;
        NotifyAll();
    }

    /// <summary>Disables Change… while the answer is being applied.</summary>
    public void SetBusy(bool busy)
    {
        _isBusy = busy;
        Notify(nameof(CanChange));
    }

    private void NotifyAll()
    {
        Notify(nameof(Path));
        Notify(nameof(PathText));
        Notify(nameof(Problem));
        Notify(nameof(HasProblem));
        Notify(nameof(Detail));
        Notify(nameof(HasDetail));
        Notify(nameof(State));
        Notify(nameof(IsFixed));
        Notify(nameof(CanChange));
        Notify(nameof(ChangeLabel));
        Notify(nameof(IsValid));
    }

    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
