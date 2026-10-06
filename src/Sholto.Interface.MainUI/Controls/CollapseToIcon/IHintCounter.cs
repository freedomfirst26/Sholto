namespace Sholto.Interface.MainUI.Controls.CollapseToIcon;

/// <summary>The saved count of how often one hint has been shown, across launches.</summary>
public interface IHintCounter
{
    /// <summary>How many times the hint has been shown so far.</summary>
    Task<int> CountAsync();

    /// <summary>The hint was shown once more; saved for the next launch.</summary>
    void Record();
}
