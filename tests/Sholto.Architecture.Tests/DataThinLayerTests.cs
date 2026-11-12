using System.Reflection;
using System.Runtime.CompilerServices;

namespace Sholto.Architecture.Tests;

/// <summary>C112: Sholto.Data is the thinnest layer — it holds contracts only. Every type declared in it must be
/// a record (class or struct), an enum, an interface or a const-only static class, and carry no logic beyond
/// that: a record or interface may declare properties but no methods of its own. The only exceptions are the
/// bus plumbing and key notation in <see cref="_allowed"/>. Types are matched by simple name (generic arity
/// dropped) so a namespace or folder move does not break the list; nested types of an allowed type are allowed.</summary>
public sealed class DataThinLayerTests
{
    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    // FROZEN (C112). Bus plumbing and key notation, plus the C218 logging types, which are pre-listed even
    // though they do not exist yet. Do not add entries: a new type in Data is a contract or belongs elsewhere.
    private readonly Assembly _data = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "Sholto.Data.dll"));

    private readonly HashSet<string> _allowed = new(StringComparer.Ordinal)
    {
        "DataBus", "Channel", "StateChannel", "Subscription", "FrameTickDispatcher",
        "ImmediateAppThread", "ManualFrameClock", "QueryAskerExtensions", "KeyRef",
        "ConsoleLog", "ConsoleLogFactory", "IConsoleLogFactory", "ILog", "ILogFactory", "LogLevel", "LogExtensions",
    };

    [Fact]
    public void DataHoldsOnlyContracts()
    {
        var offenders = _data.GetTypes()
            .Where(type => !IsCompilerGenerated(type) && !IsAllowed(type))
            .Select(type => (type, problem: ProblemWith(type)))
            .Where(hit => hit.problem is not null)
            .Select(hit => $"{hit.type.FullName}: {hit.problem}")
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToList();

        Assert.True(offenders.Count == 0,
            "Sholto.Data must hold only records, enums, interfaces and const-only static classes, with no logic. " +
            "Move the logic to Sholto.App (or an interface). Offenders:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void AllowlistedTypesWouldOtherwiseBeFlagged()
    {
        // Guards the detector against going vacuous: the plumbing it exempts is exactly what it must catch,
        // and a plain positional record must still pass.
        Assert.Equal("class that is not a record", ProblemWith(Named("DataBus")));
        Assert.Equal("record with methods: ToCamelot", ProblemWith(Named("KeyRef")));
        Assert.Null(ProblemWith(Named("DeckSection")));
    }

    /// <summary>Why <paramref name="type"/> is not a plain contract, or null when it is one.</summary>
    private string? ProblemWith(Type type)
    {
        if (type.IsEnum)
            return null;
        if (type.IsInterface)
            return MethodsWithBodies(type) is { Count: > 0 } bodies ? "interface with method bodies: " + string.Join(", ", bodies) : null;
        if (type.IsAbstract && type.IsSealed)
            return IsConstOnly(type) ? null : "static class with members other than consts";
        if (!IsRecord(type))
            return type.IsValueType ? "struct that is not a record struct" : "class that is not a record";
        return UserMethods(type) is { Count: > 0 } methods ? "record with methods: " + string.Join(", ", methods) : null;
    }

    /// <summary>The Data type with this simple name, whatever namespace it sits in.</summary>
    private Type Named(string name) => _data.GetTypes().Single(type => type.Name == name);

    private bool IsAllowed(Type type)
    {
        for (var current = type; current is not null; current = current.DeclaringType)
            if (_allowed.Contains(SimpleName(current)))
                return true;
        return false;
    }

    private string SimpleName(Type type)
    {
        var tick = type.Name.IndexOf('`');
        return tick < 0 ? type.Name : type.Name[..tick];
    }

    private bool IsCompilerGenerated(Type type) =>
        type.Name.StartsWith('<') || type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);

    /// <summary>A record class carries the synthesized <c>&lt;Clone&gt;$</c>; a record struct (which has none)
    /// carries a compiler-generated <c>PrintMembers</c>.</summary>
    private bool IsRecord(Type type) =>
        type.IsValueType
            ? type.GetMethods(Declared).Any(method => method.Name == "PrintMembers" && IsSynthesized(method))
            : type.GetMethod("<Clone>$", Declared) is not null;

    /// <summary>Methods written by hand: not property/operator accessors and not synthesized by the compiler
    /// (ToString, Equals, GetHashCode, Deconstruct, PrintMembers, ...).</summary>
    private List<string> UserMethods(Type type) =>
        type.GetMethods(Declared)
            .Where(method => !method.IsSpecialName && !IsSynthesized(method))
            .Select(method => method.Name)
            .Distinct()
            .ToList();

    private List<string> MethodsWithBodies(Type type) =>
        type.GetMethods(Declared)
            .Where(method => !method.IsAbstract)
            .Select(method => method.Name)
            .Distinct()
            .ToList();

    private bool IsConstOnly(Type type) =>
        type.GetMembers(Declared).All(member => member is FieldInfo { IsLiteral: true } || member is Type);

    private bool IsSynthesized(MemberInfo member) => member.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);
}
