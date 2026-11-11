using Microsoft.Extensions.Options;
using Sholto.Data;
using Sholto.Interface.MainUI.Controls.CollapseToIcon;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>Holds the Glance view model's dependencies and builds it: <see cref="Create"/> makes the
/// collaborators and hands them in through the constructor.</summary>
public sealed class GlanceViewModelFactory(
    ICommandSender sender, IQueryAsker asker, IEventSubscriber subscriber, IAppThread appThread,
    IFrameClock clock, IGlanceRowSource rowSource, ITagRecency tagRecency, IGlanceHeaderViewModel header,
    IMotionPreference motion, IOptions<GlanceViewOptions> options) : IGlanceViewModelFactory
{
    private readonly ICommandSender _sender = sender;
    private readonly IQueryAsker _asker = asker;
    private readonly IEventSubscriber _subscriber = subscriber;
    private readonly IAppThread _appThread = appThread;
    private readonly IFrameClock _clock = clock;
    private readonly IGlanceRowSource _rowSource = rowSource;
    private readonly ITagRecency _tagRecency = tagRecency;
    private readonly IGlanceHeaderViewModel _header = header;
    private readonly IMotionPreference _motion = motion;
    private readonly IOptions<GlanceViewOptions> _options = options;

    public GlanceViewModel Create() => new(
        _sender, _asker, _subscriber, _appThread, _clock, _rowSource, _tagRecency, _header, _motion, _options);
}
