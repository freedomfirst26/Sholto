using Sholto.Interface.MainUI.Controls.WaveformStyles;
using SkiaSharp;

namespace Sholto.Interface.MainUI.Tests.WaveformStyles;

public class RgbColourMixerTests
{
    private readonly IRgbColourMixer _mixer = new RgbColourMixer(0.75f);
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
    public void A_five_percent_high_lead_is_a_blend_nearer_the_even_mix_than_pure_blue()
    {
        var c = _mixer.Mix(1f, 1f, 1.05f, _red, _green, _blue);
        Assert.NotEqual(_blue, c);
        Assert.True(c.Red > 100 && c.Green > 100, $"expected a blend, got {c}");
    }

    [Fact]
    public void Kick_like_and_hat_like_columns_still_tint_toward_their_band()
    {
        var kick = _mixer.Mix(1.0f, 0.9f, 0.73f, _red, _green, _blue);
        Assert.True(kick.Red == 255 && kick.Red > kick.Green && kick.Red > kick.Blue, $"kick {kick}");
        var hat = _mixer.Mix(0.67f, 0.9f, 1.0f, _red, _green, _blue);
        Assert.True(hat.Blue == 255 && hat.Blue > hat.Red && hat.Blue > hat.Green, $"hat {hat}");
    }

    [Fact]
    public void A_loud_nearly_even_column_is_tinted_not_white()
    {
        var c = _mixer.Mix(1f, 0.95f, 0.9f, _red, _green, _blue);
        Assert.True(Math.Min(c.Red, Math.Min(c.Green, c.Blue)) < 200, $"too white: {c}");
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
        // Hat with mids under it is still mostly the hat's blue, not teal.
        var hat = _mixer.Mix(0f, 1f, 1f, _red, _green, _blue);
        Assert.True(hat.Blue == 255 && hat.Green < 80 && hat.Red == 0, $"hat {hat}");
        // Mids clearly on top: green leads, with the stronger of the other two blended in.
        var midLed = _mixer.Mix(0f, 1f, 0.5f, _red, _green, _blue);
        Assert.True(midLed.Green == 255 && midLed.Blue > 0 && midLed.Blue < 255 && midLed.Red == 0, $"mid-led {midLed}");
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
