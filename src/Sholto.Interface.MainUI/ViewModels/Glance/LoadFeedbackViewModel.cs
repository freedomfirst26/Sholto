using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>Projects the App's load-guard state into the replace-a-playing-deck warning, and sends the undo
/// command.</summary>
public sealed class LoadFeedbackViewModel :
    ILoadFeedbackViewModel,
    IEventHandler<LoadConfirmPending>
{
    private readonly ICommandSender _sender;

    public LoadFeedbackViewModel(ICommandSender sender, IEventSubscriber subscriber)
    {
        _sender = sender;
        subscriber.Subscribe<LoadConfirmPending>(this);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private string _warningText = "";
    public string WarningText
    {
        get => _warningText;
        private set { if (_warningText == value) return; _warningText = value; Notify(); Notify(nameof(HasWarning)); }
    }

    public bool HasWarning => _warningText.Length > 0;

    public void Undo() =>
        _sender.Send(new UndoLastLoad(new Origin(InterfaceIds.MainUI, "load-toast", "load.undo")));

    public void Handle(in LoadConfirmPending e)
    {
        WarningText = e.Pending
            ? $"Deck {e.Deck + 1} is playing · \"{e.PlayingTitle}\", {e.RemainingSeconds.ToRemainingText()} left. " +
              $"Press ⇧{e.Deck + 1} or LOAD {e.Deck + 1} again within 3 s to replace it."
            : "";
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
