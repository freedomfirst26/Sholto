namespace OpenDJ.Library;

public record Track(
    string FilePath,
    string Title,
    string Artist,
    TimeSpan Duration
);
