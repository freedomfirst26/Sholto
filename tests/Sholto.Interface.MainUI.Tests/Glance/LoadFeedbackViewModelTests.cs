using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Tests.Glance;

/// <summary>The replace warning and the undo command.</summary>
public class LoadFeedbackViewModelTests
{
    private readonly DataBus _bus = new(new ThrowingFailureSink());
    private readonly List<object> _log = [];
    private readonly LoadFeedbackViewModel _feedback;

    public LoadFeedbackViewModelTests()
    {
        _bus.Register(new LoggingCommandHandler<UndoLastLoad>(_log));
        _feedback = new LoadFeedbackViewModel(_bus, _bus);
    }

    [Fact]
    public void A_pending_replace_shows_the_warning_and_clears_with_it()
    {
        _bus.Publish(new LoadConfirmPending(true, 1, "Incoming", "Outgoing", 150));

        Assert.True(_feedback.HasWarning);
        Assert.Equal(
            "Deck 2 is playing · \"Outgoing\", −2:30 left. Press ⇧2 or LOAD 2 again within 3 s to replace it.",
            _feedback.WarningText);

        _bus.Publish(new LoadConfirmPending(false, 1, null, null, 0));
        Assert.False(_feedback.HasWarning);
    }

    [Fact]
    public void Undo_sends_the_command()
    {
        _feedback.Undo();

        Assert.Single(_log.OfType<UndoLastLoad>());
    }
}
