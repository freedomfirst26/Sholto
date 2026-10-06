using Sholto.Data;

namespace Sholto.App.Lifecycle;

/// <summary>The saved count of how often each one-time hint has been shown: records a showing
/// (<see cref="RecordHintShown"/>) and answers the count (<see cref="GetHintShownCount"/>).</summary>
public interface IHintCounter : ICommandHandler<RecordHintShown>, IQueryHandler<GetHintShownCount, Task<int>>;
