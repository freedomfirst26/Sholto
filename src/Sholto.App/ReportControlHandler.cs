using Sholto.Data;

namespace Sholto.App;

/// <summary>Handles <see cref="ReportControl"/>: a control with no effect on the App, so there is nothing
/// to do. In Inspect mode the command never gets here; the gate echoes it as <see cref="CommandReceived"/>.</summary>
public sealed class ReportControlHandler : ICommandHandler<ReportControl>
{
    public void Handle(in ReportControl command) { }
}
