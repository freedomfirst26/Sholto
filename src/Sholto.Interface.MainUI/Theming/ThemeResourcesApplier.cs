using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>Publishes a theme's "Sholto…" resources on a host's resource dictionary. The keys go in one new merged
/// dictionary that replaces the previous theme's, so the tree sees a single change instead of one per key (about 50
/// writes cost 60-76 ms of UI thread per wizard arrow key). One instance per host: it remembers its last dictionary.</summary>
public sealed class ThemeResourcesApplier
{
    private ResourceDictionary? _current;

    /// <summary>Make <paramref name="theme"/> the one the host's resources resolve, replacing the previous theme's dictionary.</summary>
    public void Apply(IResourceDictionary host, SholtoTheme theme)
    {
        var fresh = Create(theme);
        var merged = host.MergedDictionaries;
        var at = _current is null ? -1 : merged.IndexOf(_current);
        if (at >= 0) merged[at] = fresh; else merged.Add(fresh);
        _current = fresh;
    }

    private SolidColorBrush Solid(Color c) => new SolidColorBrush(c);

    private Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    /// <summary>Every themed key (brushes, palettes, shadows, tints) for <paramref name="theme"/>, in a new dictionary.</summary>
    private ResourceDictionary Create(SholtoTheme theme)
    {
        var resources = new ResourceDictionary();
        resources["SholtoBgDeep"]        = theme.BgDeep;
        resources["SholtoSurface"]       = theme.Surface;
        resources["SholtoSurfaceRaised"] = theme.SurfaceRaised;
        resources["SholtoBorder"]        = theme.Border;
        resources["SholtoPrimary"]       = theme.Primary;
        resources["SholtoAccent"]        = theme.Accent;
        resources["SholtoAccentBg"]      = theme.AccentBg;
        // Faint primary wash behind the Layout Wizard's active step.
        if (theme.Primary is SolidColorBrush primary)
            resources["SholtoPrimaryTint"] = Solid(WithAlpha(primary.Color, 0x10));
        resources["SholtoMint"]          = theme.Mint;
        resources["SholtoTextBright"]    = theme.TextBright;
        resources["SholtoTextMuted"]     = theme.TextMuted;
        // Label colour on a lit stem chip (DRMS / VOX / INST); independent of the Camelot palette.
        resources["SholtoStemChipForeground"] = new SolidColorBrush(theme.Stems.ChipText);
        // Foreground drawn on the Camelot key chips (white unless the theme sets keyChipText).
        resources["SholtoKeyChipForeground"] = theme.CamelotPalette.KeyChipForeground;
        resources["SholtoMinimapPalette"] = theme.Minimap;
        resources["SholtoWaveformPalette"] = theme.Waveform;
        resources["SholtoKnobPalette"] = theme.Knob;
        resources["SholtoTextBrightColor"] = ((SolidColorBrush)theme.TextBright).Color;

        // Stems (window icon, watermark, link icon).
        resources["SholtoStemDrums"]        = Solid(theme.Stems.Drums);
        resources["SholtoStemVocals"]       = Solid(theme.Stems.Vocals);
        resources["SholtoStemInstrumental"] = Solid(theme.Stems.Instrumental);

        // Status. Tint alphas are the ones the status pill used before theming.
        resources["SholtoStatusOk"]        = Solid(theme.Status.Ok);
        resources["SholtoStatusWarn"]      = Solid(theme.Status.Warn);
        resources["SholtoStatusError"]     = Solid(theme.Status.Error);
        resources["SholtoStatusAttention"] = Solid(theme.Status.Attention);
        resources["SholtoStatusOkTint"]    = Solid(WithAlpha(theme.Status.Ok, 0x1F));
        resources["SholtoStatusWarnTint"]  = Solid(WithAlpha(theme.Status.Warn, 0x33));
        resources["SholtoStatusErrorTint"] = Solid(WithAlpha(theme.Status.Error, 0x33));
        // "Analysis failed" marker in the track list; alias of Attention.
        resources["SholtoWarning"] = Solid(theme.Status.Attention);

        resources["SholtoMute"]        = Solid(theme.Mute);
        resources["SholtoScrim"]       = Solid(theme.Scrim);
        resources["SholtoShadow"]      = Solid(theme.Shadow);
        resources["SholtoShadowColor"] = theme.Shadow;
        resources["SholtoDeckShadow"]  = new BoxShadows(new BoxShadow
            { OffsetX = 0, OffsetY = 6, Blur = 20, Spread = 0, Color = WithAlpha(theme.Shadow, 0xA0) });
        // "This key mixes" glow on eligible Camelot key chips.
        resources["SholtoKeyChipGlow"] = new BoxShadows(new BoxShadow
            { OffsetX = 0, OffsetY = 0, Blur = 6, Spread = 1, Color = WithAlpha(((SolidColorBrush)theme.TextBright).Color, 0xB0) });
        // Empty LOAD TO slot: the card's gradient surface, the target's accent halo, and the target's soft light sweep.
        var raised = ((SolidColorBrush)theme.SurfaceRaised).Color;
        var accent = ((SolidColorBrush)theme.Accent).Color;
        resources["SholtoSlotEmptyFill"] = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
            GradientStops = [new GradientStop(raised, 0), new GradientStop(((SolidColorBrush)theme.Surface).Color, 1)],
        };
        resources["SholtoSlotHalo"] = new BoxShadows(new BoxShadow
            { OffsetX = 0, OffsetY = 0, Blur = 0, Spread = 2, Color = WithAlpha(accent, 0x33) });
        resources["SholtoSlotSweep"] = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(WithAlpha(accent, 0), 0),
                new GradientStop(WithAlpha(accent, 0x33), 0.5),
                new GradientStop(WithAlpha(accent, 0), 1),
            ],
        };
        resources["SholtoIconPlate"] = Solid(theme.IconPlate);

        // Tag editor chips + track-list tag indicator. The unprefixed keys are kept
        // because TagEditorView / the track list reference them.
        resources["SholtoTagChipBackground"]     = resources["TagChipBackground"]     = Solid(theme.Tags.ChipBg);
        resources["SholtoTagChipForeground"]     = resources["TagChipForeground"]     = Solid(theme.Tags.ChipFg);
        resources["SholtoTagIndicatorBackground"] = resources["TagIndicatorBackground"] = Solid(theme.Tags.IndicatorBg);
        resources["SholtoTagIndicatorForeground"] = resources["TagIndicatorForeground"] = Solid(theme.Tags.IndicatorFg);

        resources["SholtoFaceplateRest"]     = Solid(theme.Faceplate.Rest);
        resources["SholtoFaceplateHover"]    = Solid(theme.Faceplate.Hover);
        resources["SholtoFaceplateSelected"] = Solid(theme.Faceplate.Selected);
        resources["SholtoFaceplateGlow"]     = Solid(theme.Faceplate.Glow);
        return resources;
    }
}
