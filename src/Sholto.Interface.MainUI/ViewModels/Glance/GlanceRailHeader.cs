namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>A section header in the rail ("SHORTLIST", "RECENT LOADS", "CRATES", "TAGS") with its item count.
/// Never highlighted.</summary>
public sealed record GlanceRailHeader(string Title, int Count);
