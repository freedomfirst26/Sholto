namespace Sholto.App.Glance;

/// <summary>Creates a GlanceQuery from Glance query-language text.</summary>
public interface IGlanceQueryFactory
{
    /// <summary>Split <paramref name="text"/> into words and filters. Empty text yields a query with no constraints.</summary>
    GlanceQuery Create(string text);
}
