namespace Sholto.App.ExternalTools.Tests;

public class ExternalToolFinderTests
{
    private readonly ExternalToolOptionsFactory _optionsFactory = new(new ExternalToolCatalog());

    private string WriteFake(string dir, string name)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, "#!/bin/sh\necho fake\n");
        return path;
    }

    private ExternalToolFinder MakeFinder(IEnumerable<string> fixedDirs, string? pathEnv) =>
        new(_optionsFactory.Linux(), fixedDirs, pathEnv);

    [Fact]
    public void First_fixed_dir_wins_over_later_fixed_dirs_and_PATH()
    {
        var root = Path.Combine(Path.GetTempPath(), "sholto_resolve_" + Guid.NewGuid().ToString("N"));
        var localBin = Path.Combine(root, "local_bin");
        var usrLocalBin = Path.Combine(root, "usr_local_bin");
        var usrBin = Path.Combine(root, "usr_bin");
        var pathDir = Path.Combine(root, "path_dir");
        try
        {
            var expected = WriteFake(localBin, "sometool");
            WriteFake(usrLocalBin, "sometool");
            WriteFake(usrBin, "sometool");
            WriteFake(pathDir, "sometool");

            var finder = MakeFinder(new[] { localBin, usrLocalBin, usrBin }, pathDir);
            var found = finder.Locate("sometool");

            Assert.Equal(expected, found);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Second_fixed_dir_wins_when_first_lacks_the_binary()
    {
        var root = Path.Combine(Path.GetTempPath(), "sholto_resolve_" + Guid.NewGuid().ToString("N"));
        var localBin = Path.Combine(root, "local_bin");
        var usrLocalBin = Path.Combine(root, "usr_local_bin");
        var usrBin = Path.Combine(root, "usr_bin");
        try
        {
            Directory.CreateDirectory(localBin); // exists but has no binary
            var expected = WriteFake(usrLocalBin, "sometool");
            WriteFake(usrBin, "sometool");

            var finder = MakeFinder(new[] { localBin, usrLocalBin, usrBin }, pathEnv: null);
            var found = finder.Locate("sometool");

            Assert.Equal(expected, found);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Falls_back_to_PATH_when_no_fixed_dir_has_it()
    {
        var root = Path.Combine(Path.GetTempPath(), "sholto_resolve_" + Guid.NewGuid().ToString("N"));
        var localBin = Path.Combine(root, "local_bin");
        var usrLocalBin = Path.Combine(root, "usr_local_bin");
        var usrBin = Path.Combine(root, "usr_bin");
        var pathDir1 = Path.Combine(root, "path_dir_1");
        var pathDir2 = Path.Combine(root, "path_dir_2");
        try
        {
            Directory.CreateDirectory(localBin);
            Directory.CreateDirectory(usrLocalBin);
            Directory.CreateDirectory(usrBin);
            Directory.CreateDirectory(pathDir1); // first PATH entry, no binary
            var expected = WriteFake(pathDir2, "sometool");

            var pathEnv = string.Join(Path.PathSeparator, pathDir1, pathDir2);
            var finder = MakeFinder(new[] { localBin, usrLocalBin, usrBin }, pathEnv);
            var found = finder.Locate("sometool");

            Assert.Equal(expected, found);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Returns_null_when_not_found_anywhere()
    {
        var finder = MakeFinder(Array.Empty<string>(), pathEnv: "");
        var found = finder.Locate("sholto_definitely_does_not_exist_xyz");
        Assert.Null(found);
    }

    [Fact]
    public void LocateOrName_falls_back_to_the_suffixed_bare_name()
    {
        var finder = MakeFinder(Array.Empty<string>(), pathEnv: "");
        Assert.Equal("sholto_definitely_does_not_exist_xyz", finder.LocateOrName("sholto_definitely_does_not_exist_xyz"));
    }
}
