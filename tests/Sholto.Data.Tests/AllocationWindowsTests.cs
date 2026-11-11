using Sholto.TestSupport.Allocation;

namespace Sholto.Data.Tests;

public class AllocationWindowsTests
{
    private object? _sink;

    [Fact]
    public void A_window_that_allocates_every_time_has_a_non_zero_smallest()
    {
        var result = new AllocationWindows(5).Measure(() =>
        {
            for (var i = 0; i < 100; i++)
            {
                _sink = new object();
            }
        });

        Assert.Equal(5, result.Bytes.Count);
        Assert.True(result.Smallest > 0, result.ToString());
        Assert.All(result.Bytes, b => Assert.True(b > 0, result.ToString()));
        Assert.Equal(5, result.AllocatingWindows);
    }

    [Fact]
    public void A_window_that_allocates_only_the_first_time_has_one_allocating_window()
    {
        var runs = 0;
        var result = new AllocationWindows(5).Measure(() =>
        {
            if (runs++ == 0)
            {
                _sink = new byte[1_000];
            }
        });

        Assert.True(result.Bytes[0] > 0, result.ToString());
        Assert.Equal(0, result.Smallest);
        Assert.Equal(1, result.AllocatingWindows);
    }

    [Fact]
    public void A_list_that_grows_by_doubling_allocates_in_more_than_one_window()
    {
        var list = new List<int>();
        for (var i = 0; i < 10; i++)
        {
            list.Add(i);
        }

        var result = new AllocationWindows(5).Measure(() =>
        {
            for (var i = 0; i < 1_000; i++)
            {
                list.Add(i);
            }
        });

        // The smallest-window rule alone would pass this: window 4 (3,000 -> 4,000 items) does not regrow.
        Assert.Equal(0, result.Smallest);
        Assert.True(result.AllocatingWindows >= 2, result.ToString());
    }

    [Fact]
    public void A_window_that_allocates_nothing_has_at_most_one_allocating_window()
    {
        var counter = 0;
        var windows = new AllocationWindows(5);
        windows.Measure(() => counter++);

        var result = windows.Measure(() => counter++);

        Assert.True(result.AllocatingWindows <= 1, result.ToString());
    }
}
