namespace Sholto.Interface.MainUI.Controls.CollapseToIcon;

public sealed class CollapseToIconTimingsFactory : ICollapseToIconTimingsFactory
{
    public CollapseToIconTimings Standard() => new(
        TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(260),
        TimeSpan.FromMilliseconds(900), 3,
        TimeSpan.FromMilliseconds(150), TimeSpan.FromSeconds(4));
}
