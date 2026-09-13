using Xunit;
using Sholto.App;

namespace Sholto.App.Tests;

public class StemPathHashTests
{
    [Fact]
    public void SamePath_HashesSame_BothTimes()
    {
        string a = ExternalToolStack.HashPath("/Music/Golden Track.mp3");
        string b = ExternalToolStack.HashPath("/Music/Golden Track.mp3");
        Assert.Equal(a, b);
    }

    [Fact]
    public void Result_Is16UppercaseHexChars()
    {
        string hash = ExternalToolStack.HashPath("/Music/Golden Track.mp3");
        Assert.Equal(16, hash.Length);
        Assert.Matches("^[0-9A-F]{16}$", hash);
    }

    [Fact]
    public void SpaceVsUnderscore_ProduceDifferentHashes()
    {
        // This is the exact collision the change was made to fix: the old
        // "replace unsafe chars with _" scheme mapped both ' ' and '_' to '_',
        // so these two distinct tracks collided into one stem directory and
        // the second track silently inherited the first's stems. The hash
        // encoding must keep them apart.
        string spaceHash = ExternalToolStack.HashPath("/Music/My Track.mp3");
        string underscoreHash = ExternalToolStack.HashPath("/Music/My_Track.mp3");
        Assert.NotEqual(spaceHash, underscoreHash);
    }

    [Fact]
    public void LongPath_StillProduces16CharResult()
    {
        string deepPath = "/" + string.Join('/', Enumerable.Repeat("a-very-long-library-folder-name", 20)) + "/track.mp3";
        string hash = ExternalToolStack.HashPath(deepPath);
        Assert.Equal(16, hash.Length);
    }

    [Fact]
    public void KnownInput_HashesToPinnedValue()
    {
        // CONTRACT: this encoding names every stem directory on disk. Changing
        // HashPath's algorithm orphans every user's cached stems and forces
        // demucs to re-run across their entire library. Do not change this
        // expected value without accepting that consequence.
        //
        // Input is already an absolute Linux path, so Path.GetFullPath is a
        // no-op and this assertion doesn't depend on the working directory.
        // Expected value computed by running the real HashPath expression
        // (SHA256 of the UTF-8 bytes of "/Music/Golden Track.mp3", truncated
        // to 16 hex chars, uppercase) — not invented.
        string hash = ExternalToolStack.HashPath("/Music/Golden Track.mp3");
        Assert.Equal("207A951272CE0E65", hash);
    }
}
