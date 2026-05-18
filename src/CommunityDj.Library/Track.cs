namespace CommunityDj.Library;

public sealed record Track(
    string FilePath,
    string Title,
    string Artist,
    TimeSpan Duration
);
