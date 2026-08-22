using SoundFlow.Abstracts;
using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.Audio;

/// <summary>
/// Top-level 4-channel output component. Replaces "add each deck to the master
/// mixer": instead this single component is the device's source and it PULLS
/// each deck's post-EQ stereo (via the public <see cref="SoundComponent.Process"/>)
/// once per buffer, then composes the device output:
///
///   ch1-2 (master)     = Σ deck.postEq × deck.MasterGain   (channel × crossfade)
///   ch3-4 (headphones) = Σ deck.postEq × (deck.CueActive ? 1 : 0)   (PFL — pre-fader)
///
/// The cue mix is written only when the device has ≥4 channels (the FLX4). On a
/// 2-channel device it degrades to master-only, exactly as before.
///
/// Why pull instead of graph fan-out: SoundFlow sums components, it does not let
/// one component's output land on specific output channels. Pulling each deck
/// here gives per-deck buffers we can place on master vs cue independently, and
/// the fader/crossfader now live on the master path only so the cue tap is
/// genuinely pre-fader.
/// </summary>
internal sealed class CueOutputRouter : SoundComponent
{
    private readonly IReadOnlyList<Deck> _decks;
    private float[] _scratch = [];

    public CueOutputRouter(SfEngine engine, AudioFormat format, IReadOnlyList<Deck> decks)
        : base(engine, format)
    {
        _decks = decks;
    }

    public override string Name { get; set; } = "CueOutputRouter";

    protected override void GenerateAudio(Span<float> buffer, int channels)
    {
        buffer.Clear();
        if (channels <= 0) return;

        int frames = buffer.Length / channels;
        int need = frames * 2; // deck output is stereo
        if (_scratch.Length < need) _scratch = new float[need];
        var stereo = _scratch.AsSpan(0, need);
        bool hasCue = channels >= 4;

        foreach (var deck in _decks)
        {
            // Pull this deck's post-EQ, full-level stereo (fader NOT applied —
            // the SoundPlayer runs at unity; MasterGain is applied below).
            stereo.Clear();
            deck.Component.Process(stereo, 2);

            float mg = deck.MasterGain;           // channel × crossfade
            float cg = deck.CueActive ? 1f : 0f;  // PFL: full level, pre-fader
            if (mg == 0f && cg == 0f) continue;

            MixDeckInto(buffer, stereo, frames, channels, mg, cg);
        }
    }

    /// <summary>Accumulate one deck's stereo into the interleaved device buffer:
    /// master (× <paramref name="masterGain"/>) on ch1-2, and — when the device
    /// has ≥4 channels — the pre-fader cue (× <paramref name="cueGain"/>) on
    /// ch3-4. Cue uses <paramref name="cueGain"/> only (never the fader), so a
    /// cued deck with its fader down is silent on master yet full on the cue.</summary>
    internal static void MixDeckInto(
        Span<float> buffer, ReadOnlySpan<float> stereo, int frames, int channels,
        float masterGain, float cueGain)
    {
        bool hasCue = channels >= 4;
        for (int f = 0; f < frames; f++)
        {
            float l = stereo[f * 2];
            float r = stereo[f * 2 + 1];
            int o = f * channels;
            buffer[o]     += l * masterGain; // master L (ch1)
            buffer[o + 1] += r * masterGain; // master R (ch2)
            if (hasCue)
            {
                buffer[o + 2] += l * cueGain; // cue L (ch3) → headphones
                buffer[o + 3] += r * cueGain; // cue R (ch4) → headphones
            }
        }
    }
}
