using System.Text;
using System.Text.RegularExpressions;

namespace Sholto.Architecture.Tests;

/// <summary>Finds, in one file's text, the same-file async methods that are fired and forgotten with an unqualified
/// <c>_ = Name(...)</c> call while their body has no top-level try plus catch.
/// Known false negatives: a top-level catch that rethrows or catches only a narrow type counts as guarded; qualified
/// calls, <c>_ = Task.Run(...)</c> lambdas, cross-file targets, generic methods (<c>FooAsync&lt;T&gt;</c>), tuple return types
/// and <c>async void</c> methods are not examined.</summary>
public sealed partial class UnguardedFireAndForgetScanner
{
    // The lookbehind drops "x._ =" and the unqualified name group drops "_ = a.B(".
    [GeneratedRegex(@"(?<![\w.])_\s*=\s*(?<name>[A-Za-z_]\w*)\s*\(")]
    private static partial Regex DiscardCall();

    [GeneratedRegex(@"\btry\s*\{")]
    private static partial Regex TryBlock();

    [GeneratedRegex(@"\bcatch\b\s*(?:[({]|when\b)")]
    private static partial Regex CatchClause();

    /// <summary>The distinct names of fired-and-forgotten async methods with no top-level try/catch, sorted ordinal.</summary>
    public IReadOnlyList<string> UnguardedTargets(string sourceText)
    {
        var names = DiscardCall().Matches(sourceText).Select(match => match.Groups["name"].Value).Distinct();
        return names
            .Where(name => Declarations(sourceText, name).Any(declaration => !IsGuarded(sourceText, declaration)))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    // The index just after the "(" of each "async ... name(" declaration in the text.
    private static IEnumerable<int> Declarations(string text, string name) =>
        Regex.Matches(text, @"\basync\b[^;{=()]*?\b" + Regex.Escape(name) + @"\s*\(")
            .Select(match => match.Index + match.Length);

    private static bool IsGuarded(string text, int afterOpenParen)
    {
        var index = SkipBalanced(text, afterOpenParen, '(', ')');
        while (index >= 0 && index < text.Length && text[index] != '{' && !(text[index] == '=' && index + 1 < text.Length && text[index + 1] == '>'))
        {
            index++;
        }

        if (index < 0 || index >= text.Length || text[index] != '{')
        {
            return false; // expression-bodied, or no body found
        }

        var topLevel = new StringBuilder();
        var depth = 0;
        for (; index < text.Length; index++)
        {
            var c = text[index];
            if (c == '{')
            {
                depth++;
                if (depth == 2)
                {
                    topLevel.Append(c);
                }
            }
            else if (c == '}')
            {
                if (depth == 2)
                {
                    topLevel.Append(c);
                }

                depth--;
                if (depth == 0)
                {
                    return TryBlock().IsMatch(topLevel.ToString()) && CatchClause().IsMatch(topLevel.ToString());
                }
            }
            else if (depth == 1)
            {
                topLevel.Append(c);
            }
        }

        return false; // the body never closes (a stray brace in a literal): treat as unguarded
    }

    // Returns the index after the matching close, or -1 when the text ends first.
    private static int SkipBalanced(string text, int start, char open, char close)
    {
        var depth = 1;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == open)
            {
                depth++;
            }
            else if (text[i] == close && --depth == 0)
            {
                return i + 1;
            }
        }

        return -1;
    }
}
