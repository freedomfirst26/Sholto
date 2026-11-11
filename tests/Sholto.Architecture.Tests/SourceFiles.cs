namespace Sholto.Architecture.Tests;

/// <summary>Finds the production C# files under src/, starting from the folder that holds Sholto.slnx.</summary>
public sealed class SourceFiles
{
    private static readonly string[] SkippedFolders = ["bin", "obj", "Migrations"];

    /// <summary>The repo root: the nearest folder above the test's base directory that holds Sholto.slnx.</summary>
    public string RepoRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "Sholto.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            $"Sholto.slnx not found in or above {AppContext.BaseDirectory}.");
    }

    /// <summary>Every src/**/*.cs file, leaving out bin and obj, Migrations folders and *.axaml.cs code-behind.</summary>
    public IReadOnlyList<string> All()
    {
        var src = Path.Combine(RepoRoot(), "src");
        return Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(".axaml.cs", StringComparison.Ordinal))
            .Where(path => !Path.GetRelativePath(src, path)
                .Split(Path.DirectorySeparatorChar)
                .SkipLast(1)
                .Any(part => SkippedFolders.Contains(part, StringComparer.Ordinal)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }
}
