using System.Reflection;

namespace Sholto.Architecture.Tests;

/// <summary>Reads which assemblies a built Sholto assembly references. The assemblies are loaded from the test's
/// output folder, where the project references copy them; nothing in them is run.</summary>
public sealed class AssemblyReferences
{
    private readonly string _folder = AppContext.BaseDirectory;

    /// <summary>The simple names of every assembly <paramref name="assemblyName"/> references directly.</summary>
    public IReadOnlyList<string> Of(string assemblyName) =>
        Assembly.LoadFrom(Path.Combine(_folder, assemblyName + ".dll"))
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToList();

    /// <summary>The names of every Sholto.App.* assembly built next to the tests (test projects excluded), so a
    /// new sub-project is covered without editing a list.</summary>
    public IReadOnlyList<string> AppSubProjects() =>
        Directory.GetFiles(_folder, "Sholto.App.*.dll")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Where(name => !name.EndsWith(".Tests", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    /// <summary>True when <paramref name="reference"/> is <paramref name="prefix"/> itself or one of its
    /// dotted children (so "Sholto.App" matches "Sholto.App.Audio" but not "Sholto.Application").</summary>
    public bool IsUnder(string reference, string prefix) =>
        reference == prefix || reference.StartsWith(prefix + ".", StringComparison.Ordinal);
}
