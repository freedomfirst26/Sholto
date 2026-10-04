namespace Sholto.App.ExternalTools.Tests;

/// <summary>
/// The failure path that a broken demucs exposed: the analyser's own error output was
/// discarded by the progress parser, and a Failed step was indistinguishable from a
/// success because nothing read anything but IsBusy.
/// </summary>
public class ProcessOutputTailTests
{
    [Fact]
    public void Keeps_only_the_last_N_lines()
    {
        var tail = new ProcessOutputTail(capacity: 3);
        for (var i = 1; i <= 10; i++) tail.Add($"line {i}");

        Assert.Equal("line 8\nline 9\nline 10", tail.Text);
    }

    [Fact]
    public void Ignores_blank_lines()
    {
        var tail = new ProcessOutputTail();
        tail.Add(null);
        tail.Add("");
        tail.Add("   ");

        Assert.True(tail.IsEmpty);
    }

    [Fact]
    public void Truncates_a_single_enormous_line()
    {
        var tail = new ProcessOutputTail();
        tail.Add(new string('x', 100_000));

        Assert.True(tail.Text.Length <= ProcessOutputTail.MaxLineLength + 1);
    }

    [Fact]
    public void Annotate_appends_captured_output_and_leaves_the_message_alone_when_empty()
    {
        var empty = new ProcessOutputTail();
        Assert.Equal("boom", empty.Annotate("boom"));

        var tail = new ProcessOutputTail();
        tail.Add("Traceback (most recent call last):");
        tail.Add("ImportError: TorchCodec is required for save_with_torchcodec");

        var annotated = tail.Annotate("demucs exited with code 1");
        Assert.StartsWith("demucs exited with code 1", annotated);
        Assert.Contains("ImportError: TorchCodec is required", annotated);
    }

}
