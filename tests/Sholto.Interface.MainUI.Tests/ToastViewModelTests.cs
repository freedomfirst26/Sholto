using Sholto.Interface.MainUI.ViewModels;
using Sholto.TestSupport;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>The toast hides itself a moment after it is shown, and the clear comes back through the app
/// thread rather than being set from the thread the wait ran on.</summary>
public class ToastViewModelTests
{
    [Fact]
    public void A_toast_clears_only_through_the_app_thread()
    {
        var thread = new QueuedAppThread();
        var toast = new ToastViewModel(thread);

        toast.Show("Hi");

        Assert.Equal("Hi", toast.Toast);
        Assert.True(toast.HasToast);
        // The wait is not injectable, so poll for the post it ends with.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (thread.Pending == 0 && DateTime.UtcNow < deadline) Thread.Sleep(10);
        Assert.Equal(1, thread.Pending);
        Assert.Equal("Hi", toast.Toast);

        thread.Drain();

        Assert.Null(toast.Toast);
        Assert.False(toast.HasToast);
    }
}
