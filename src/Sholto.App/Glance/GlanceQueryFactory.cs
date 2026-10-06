using System.Globalization;
using System.Text.RegularExpressions;

namespace Sholto.App.Glance;

/// <inheritdoc cref="IGlanceQueryFactory"/>
public sealed class GlanceQueryFactory : IGlanceQueryFactory
{
    private readonly Regex _bpm = new(@"^bpm:(\d{2,3})(?:-(\d{2,3}))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private readonly Regex _key = new(@"^key:(\d{1,2}[ab])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public GlanceQuery Create(string text)
    {
        var words = new List<string>();
        var tags = new List<string>();
        double? bpmMin = null;
        double? bpmMax = null;
        string? bpmLabel = null;
        string? key = null;

        foreach (string word in (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            Match m;
            if ((m = _bpm.Match(word)).Success)
            {
                double a = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                if (m.Groups[2].Success)
                {
                    double b = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                    bpmMin = Math.Min(a, b);
                    bpmMax = Math.Max(a, b);
                    bpmLabel = $"BPM {a.ToString(CultureInfo.InvariantCulture)}–{b.ToString(CultureInfo.InvariantCulture)}";
                }
                else
                {
                    bpmMin = a - 0.5;
                    bpmMax = a + 0.5;
                    bpmLabel = $"BPM {a.ToString(CultureInfo.InvariantCulture)}";
                }
            }
            else if ((m = _key.Match(word)).Success)
            {
                key = m.Groups[1].Value.ToUpperInvariant();
            }
            else if (word.Length > 1 && word[0] == '#')
            {
                tags.Add(word[1..].ToLowerInvariant());
            }
            else
            {
                words.Add(word.ToLowerInvariant());
            }
        }

        var chips = new List<string>();
        if (bpmLabel is not null) chips.Add(bpmLabel);
        if (key is not null) chips.Add("KEY " + key);
        foreach (string t in tags) chips.Add("#" + t);

        return new GlanceQuery(words, bpmMin, bpmMax, key, tags, chips);
    }
}
