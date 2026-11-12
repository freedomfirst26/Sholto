namespace Sholto.App.Tests;

/// <summary>Lays down the demucs layout, <c>&lt;root&gt;/stems/&lt;hash&gt;/htdemucs/{vocals,drums,bass,other}.wav</c>.</summary>
internal sealed class StemTreeBuilder
{
    private readonly string[] StemNames = ["vocals", "drums", "bass", "other"];

    /// <summary>Add one track folder with the four stems, each <paramref name="bytes"/> long. Returns the track folder.</summary>
    public string Add(string root, string hash, int bytes = 100)
    {
        var dir = Path.Combine(root, "stems", hash);
        Directory.CreateDirectory(Path.Combine(dir, "htdemucs"));
        foreach (var name in StemNames)
            File.WriteAllBytes(Path.Combine(dir, "htdemucs", name + ".wav"), new byte[bytes]);
        return dir;
    }
}
