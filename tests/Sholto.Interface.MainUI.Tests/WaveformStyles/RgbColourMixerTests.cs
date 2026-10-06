using Sholto.Interface.MainUI.Controls.WaveformStyles;
using SkiaSharp;

namespace Sholto.Interface.MainUI.Tests.WaveformStyles;

public class RgbColourMixerTests
{
    private readonly IRgbColourMixer _mixer = new RgbColourMixer();
    private readonly SKColor _red = new(255, 0, 0), _green = new(0, 255, 0), _blue = new(0, 0, 255);

    [Fact]
    public void A_single_band_gives_that_bands_colour()
    {
        Assert.Equal(_red, _mixer.Mix(0.4f, 0f, 0f, _red, _green, _blue));
        Assert.Equal(_blue, _mixer.Mix(0f, 0f, 0.05f, _red, _green, _blue));
        Assert.Equal(_green, _mixer.Mix(0f, 0.7f, 0f, _red, _green, _blue));
    }

    [Fact]
    public void Equal_bands_give_white_with_primaries()
    {
        Assert.Equal(new SKColor(255, 255, 255), _mixer.Mix(0.3f, 0.3f, 0.3f, _red, _green, _blue));
    }

    [Fact]
    public void Colour_shows_the_mix_not_the_level()
    {
        // Same proportions at different levels → same colour; loudness is the stroke's job.
        Assert.Equal(_mixer.Mix(0.2f, 0f, 0.1f, _red, _green, _blue), _mixer.Mix(1f, 0f, 0.5f, _red, _green, _blue));
        Assert.Equal(_mixer.Mix(3f, 0f, 1.5f, _red, _green, _blue), _mixer.Mix(1f, 0f, 0.5f, _red, _green, _blue));
    }

    [Fact]
    public void Only_a_bands_lead_over_the_weakest_counts_so_a_loud_column_is_not_white()
    {
        // All bands near their track reference, as in a drop: the kick column leads on bass, the hat
        // column on treble, and each reads as that band alone.
        Assert.Equal(_red, _mixer.Mix(1.0f, 0.9f, 0.73f, _red, _green, _blue));
        Assert.Equal(_blue, _mixer.Mix(0.67f, 0.9f, 1.0f, _red, _green, _blue));
    }

    [Fact]
    public void The_lead_is_sharpened_so_a_two_to_one_lead_reads_four_to_one()
    {
        // Leads (1, 0, 0.5) → squared (1, 0, 0.25): linear mixing would give (255, 0, 128).
        Assert.Equal(new SKColor(255, 0, 64), _mixer.Mix(1f, 0f, 0.5f, _red, _green, _blue));
        var kick = _mixer.Mix(1f, 0.1f, 0.3f, _red, _green, _blue);
        Assert.True(kick.Red == 255 && kick.Blue < 20 && kick.Green == 0, $"kick {kick}");
        var hat = _mixer.Mix(0.3f, 0.1f, 1f, _red, _green, _blue);
        Assert.True(hat.Blue == 255 && hat.Red < 20 && hat.Green == 0, $"hat {hat}");
    }

    [Fact]
    public void Kick_plus_hat_is_magenta()
    {
        Assert.Equal(new SKColor(255, 0, 255), _mixer.Mix(1f, 0f, 1f, _red, _green, _blue));
        Assert.Equal(new SKColor(255, 0, 255), _mixer.Mix(1f, 0.6f, 1f, _red, _green, _blue));
    }

    [Fact]
    public void Mids_colour_a_column_only_when_they_lead_both_other_bands()
    {
        // Hat with mids under it is still the hat's blue, not teal.
        Assert.Equal(_blue, _mixer.Mix(0f, 1f, 1f, _red, _green, _blue));
        Assert.Equal(_blue, _mixer.Mix(0.5f, 0.9f, 1f, _red, _green, _blue));
        // Mids clearly on top: green, mixed with the stronger of the other two.
        Assert.Equal(new SKColor(0, 255, 255), _mixer.Mix(0f, 1f, 0.5f, _red, _green, _blue));
        var midLed = _mixer.Mix(0.2f, 1f, 0.4f, _red, _green, _blue);
        Assert.True(midLed.Green == 255 && midLed.Green > midLed.Blue && midLed.Red == 0, $"mid-led {midLed}");
    }

    [Fact]
    public void The_brightest_channel_is_always_255_and_opaque()
    {
        var c = _mixer.Mix(0.1f, 0.7f, 0.3f, new SKColor(200, 40, 10), new SKColor(30, 180, 60), new SKColor(20, 90, 220));
        Assert.Equal(255, Math.Max(c.Red, Math.Max(c.Green, c.Blue)));
        Assert.Equal(255, c.Alpha);
    }

    [Fact]
    public void Silence_and_negative_weights_are_transparent()
    {
        Assert.Equal(SKColors.Transparent, _mixer.Mix(0f, 0f, 0f, _red, _green, _blue));
        Assert.Equal(SKColors.Transparent, _mixer.Mix(-1f, -0.5f, 0f, _red, _green, _blue));
    }
}
