using System.Reflection;
using System.Runtime.CompilerServices;

namespace Sholto.Architecture.Tests;

/// <summary>CLAUDE.md: no statics except extension methods and what the language or a framework forces. Every
/// other static method, field or property in the production assemblies must be in <see cref="Allowed"/>.</summary>
public sealed class NoStaticsTests
{
    // Sholto.Host (the exe) is not scanned: the csproj deliberately does not reference it, and its only statics
    // are Program.Main / BuildAvaloniaApp, which are exempt anyway.
    private const BindingFlags AllStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    // FROZEN: today's real hits ("Namespace.Type.Member"), sorted. It may only shrink: remove an entry when
    // its static goes away (the stale-entry test fails until you do). Never add one.
    private static readonly string[] Allowed =
    [
        "Sholto.App.Analysis.ToolBoundary.ToolOutcome`1.Failure",
        "Sholto.App.Analysis.ToolBoundary.ToolOutcome`1.Success",
        "Sholto.App.Audio.CueOutputRouter.MixDeckInto",
        "Sholto.App.Audio.DjFilter.FilterCoeffs.Bypass",
        "Sholto.App.Audio.ScratchDataProvider.WriteInterpolatedFrame",
        "Sholto.App.Audio.StemMixDataProvider.WriteInterpolatedFrame",
        "Sholto.App.Lifecycle.AudioOutput.LogRoute",
    ];

    [Fact]
    public void NoStaticsOutsideTheAllowlist()
    {
        var offenders = Found().Where(hit => !Allowed.Contains(hit)).ToList();

        Assert.True(offenders.Count == 0,
            "Statics are forbidden (extension methods and framework-forced members excepted). Offenders:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void EveryAllowlistEntryStillExists()
    {
        var found = Found().ToHashSet();
        var stale = Allowed.Where(entry => !found.Contains(entry)).ToList();

        Assert.True(stale.Count == 0,
            "Allowlist entries no longer exist; remove them so the list only shrinks:\n" + string.Join("\n", stale));
    }

    private static List<string> Found()
    {
        var folder = AppContext.BaseDirectory;
        var names = Directory.GetFiles(folder, "Sholto.*.dll")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Where(name => !name.EndsWith(".Tests", StringComparison.Ordinal) && !name.Contains("TestSupport", StringComparison.Ordinal)
                           && !name.Contains("Harness", StringComparison.Ordinal))
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal);

        var hits = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            var assembly = Assembly.LoadFrom(Path.Combine(folder, name + ".dll"));
            foreach (var type in Types(assembly))
                foreach (var member in StaticMembers(type))
                    hits.Add($"{type.FullName!.Replace('+', '.')}.{member}");
        }
        return hits.ToList();
    }

    private static IEnumerable<Type> Types(Assembly assembly)
    {
        Type[] types;
        try { types = assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray()!; }
        return types.Where(t => !IsGenerated(t) && !InMigrations(t) && t.Name != "Program");
    }

    private static IEnumerable<string> StaticMembers(Type type)
    {
        foreach (var field in type.GetFields(AllStatic))
        {
            if (field.IsLiteral || IsGenerated(field) || field.Name.Contains('<')) continue;
            if (IsAvaloniaRegistration(field.FieldType)) continue;
            yield return field.Name;
        }
        foreach (var property in type.GetProperties(AllStatic))
        {
            if (IsGenerated(property)) continue;
            yield return property.Name;
        }
        foreach (var method in type.GetMethods(AllStatic))
        {
            if (method.IsSpecialName || IsGenerated(method) || method.Name.Contains('<')) continue;
            if (method.IsDefined(typeof(ExtensionAttribute), false)) continue;
            if (IsAvaloniaAccessor(method)) continue;
            yield return method.Name;
        }
    }

    private static bool IsGenerated(MemberInfo member) =>
        member.IsDefined(typeof(CompilerGeneratedAttribute), false) || member.Name.Contains('<');

    private static bool InMigrations(Type type) =>
        (type.Namespace ?? string.Empty).Split('.').Contains("Migrations");

    // Static registration fields: StyledProperty/DirectProperty/AttachedProperty (AvaloniaProperty) and RoutedEvent.
    private static bool IsAvaloniaRegistration(Type type)
    {
        for (var t = type; t != null; t = t.BaseType)
            if (t.FullName is "Avalonia.AvaloniaProperty" or "Avalonia.Interactivity.RoutedEvent") return true;
        return false;
    }

    // Attached-property accessors: static Get*/Set* taking an AvaloniaObject (or subclass) first.
    private static bool IsAvaloniaAccessor(MethodInfo method)
    {
        if (!method.Name.StartsWith("Get", StringComparison.Ordinal) && !method.Name.StartsWith("Set", StringComparison.Ordinal)) return false;
        var first = method.GetParameters().FirstOrDefault();
        for (var t = first?.ParameterType; t != null; t = t.BaseType)
            if (t.FullName == "Avalonia.AvaloniaObject") return true;
        return false;
    }
}
