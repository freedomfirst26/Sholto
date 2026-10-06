namespace Sholto.Interface.MainUI.Tests;

/// <summary>
/// Avalonia 11.3 parses reflection-binding paths into one process-wide list
/// (<c>BindingExpressionGrammar.s_pool</c>), which is safe only on a single UI thread.
/// Under the headless <c>NullDispatcherImpl</c> every thread counts as the UI thread, so test
/// classes that build XAML in parallel corrupt it ("Collection was modified" in
/// <c>ExpressionNodeFactory.CreateFromAst</c>).
/// Classes in this collection run one at a time, and every test class that builds a XAML view
/// or a <c>Binding</c> must join it.
/// </summary>
[CollectionDefinition(Name)]
public sealed class AvaloniaXamlCollection
{
    public const string Name = "Avalonia XAML";
}
