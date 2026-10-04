namespace Sholto.App.ExternalTools.Tests;

public class ExternalToolOptionsTests
{
    private readonly ExternalToolOptionsFactory _optionsFactory = new(new ExternalToolCatalog());

    [Fact]
    public void ForCurrentPlatform_returns_Linux_options_on_Linux()
    {
        // This suite only needs to run on Linux (per CLAUDE.md); assert Linux()
        // directly and separately assert ForCurrentPlatform delegates correctly
        // without requiring the test itself to run on Windows.
        var linux = _optionsFactory.Linux();
        Assert.Equal("", linux.ExecutableSuffix);
        Assert.Contains(linux.SearchDirectories, d => d.EndsWith("/.local/bin"));
        Assert.Contains("/usr/local/bin", linux.SearchDirectories);
        Assert.Contains("/usr/bin", linux.SearchDirectories);

        if (!OperatingSystem.IsWindows())
        {
            var current = _optionsFactory.ForCurrentPlatform();
            Assert.Equal(linux.ExecutableSuffix, current.ExecutableSuffix);
            Assert.Equal(linux.SearchDirectories, current.SearchDirectories);
        }
    }

    [Fact]
    public void Windows_options_append_dot_exe_and_search_windows_dirs()
    {
        // A Windows-shaped options object, built and exercised without needing to
        // actually run on Windows.
        var windows = _optionsFactory.Windows();
        Assert.Equal(".exe", windows.ExecutableSuffix);
        Assert.NotEmpty(windows.SearchDirectories);
        Assert.All(windows.SearchDirectories, d => Assert.DoesNotContain("/usr/", d));

        var finder = new ExternalToolFinder(
            windows, windows.SearchDirectories, pathEnv: null);
        var dir = windows.SearchDirectories[0];
        Directory.CreateDirectory(dir);
        var fake = Path.Combine(dir, "demucs.exe");
        try
        {
            File.WriteAllText(fake, "fake");
            var found = finder.Locate("demucs");
            Assert.Equal(fake, found);
        }
        finally
        {
            File.Delete(fake);
        }
    }

    [Fact]
    public void Defaults_carry_every_tool_name()
    {
        // The old MadmomName/DemucsName/FfmpegName scalars (and
        // DemucsModelDir) are gone from ExternalToolOptions — the tool list is
        // now a single collection, and the demucs model dir moved to
        // DemucsTool.ModelDir (an adapter-owned output-layout fact, not a
        // machine fact this type should carry). The list itself now comes from
        // ExternalToolCatalog.
        var options = _optionsFactory.Linux();
        Assert.Equal(new ExternalToolCatalog().All, options.Tools);
    }
}
