using Sholto.Interface.Keyboard;
using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;
using Avalonia.Controls.Selection;
using Avalonia.Input;
using Avalonia.Interactivity;
using Sholto.Interface.MainUI.Controls;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;
using Sholto.App.Audio;
using Sholto.Interface.Faceplate.Views;
using Sholto.App.Library;

namespace Sholto.Interface.MainUI.Views;

public partial class MainWindow : Window, IKeyboard
{
    /// <summary>Raised for every key that reaches the gesture stage — see
    /// <see cref="IKeyboard"/>'s doc. The composition root's handler recognises the
    /// key as a gesture (or not) and sets <c>Handled</c> synchronously. Everything else
    /// this window handles (search, dialogs, the tag editor, list navigation, …) is
    /// UI chrome and stays entirely in <see cref="OnGlobalKeyDown"/>, before this
    /// is raised.</summary>
    public event Action<KeyEventArgs>? KeyPressed;

    private readonly FaceplateOverlay _faceplateOverlay;
    private readonly IThemeCatalog _themeCatalog;

    /// <summary>The overlay is built by the composition root (it needs a device's guide and
    /// drawing, which XAML cannot supply) and mounted into <c>FaceplateHost</c> here.
    /// Like the tag editor, the overlay binds its OWN IsVisible to the host's so its
    /// code-behind sees the change. It builds and owns its FaceplateViewModel; no
    /// DataContext is set on it from here, or the panel would silently stop opening.</summary>
    public MainWindow(FaceplateOverlay faceplateOverlay, IThemeCatalog themeCatalog)
    {
        _faceplateOverlay = faceplateOverlay;
        _themeCatalog = themeCatalog;
        // Classic is applied before InitializeComponent so every {DynamicResource Sholto…}
        // resolves on first render; the XAML carries no default colours of its own.
        var classic = _themeCatalog.ByName("Classic");
        ApplyThemeToResources(classic);
        InitializeComponent();
        FaceplateHost.Children.Add(_faceplateOverlay);
        _faceplateOverlay.Bind(IsVisibleProperty, FaceplateHost.GetObservable(IsVisibleProperty));
        Icon = BuildAppIcon(classic);   // tri-colour RGB "S" on a rounded plate — matches the library watermark
        BuildThemesMenu();
        // Intercept keys before child controls (ListBox would otherwise eat arrows).
        AddHandler(KeyDownEvent, OnGlobalKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);

        // Push the initial theme into the Window's dynamic-resource brushes so the
        // first paint already has the right colors.
        DataContextChanged += (_, _) =>
        {
            if (DataContext is not MainViewModel vm) return;
            ApplyThemeToResources(vm.Theme);
            // Hand the view model the SAME FaceplateViewModel the mounted overlay
            // already built for itself (see the constructor) — not a second one, so a future caller
            // driving Faceplate.Select from a live gesture actually reaches the control that's on screen.
            vm.AttachFaceplate(_faceplateOverlay.ViewModel);
        };
    }

    /// <summary>Rasterise the Sholto brand mark for the window / taskbar / alt-tab
    /// icon: a rounded-square plate with three offset "S" glyphs (blue/green/red,
    /// screen-blended) — the same RGB-split S as the Media Library watermark.</summary>
    private static WindowIcon BuildAppIcon(SholtoTheme theme)
    {
        const int size = 256;
        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        // Rounded-square plate (Tokyo Night surface).
        using (var plate = new SKPaint { Color = ToSk(theme.IconPlate), IsAntialias = true })
            canvas.DrawRoundRect(new SKRect(0, 0, size, size), 56, 56, plate);

        using var tf = SKTypeface.FromFamilyName("Inter",
                           SKFontStyleWeight.ExtraBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
                       ?? SKTypeface.FromFamilyName("Arial",
                           SKFontStyleWeight.ExtraBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
                       ?? SKTypeface.Default;
        // Measure the glyph's TIGHT bounds (SKTextBlob.Bounds is a loose, inflated
        // box that threw the centring way off), then draw with TextAlign.Center so
        // the horizontal centre is just cx and only the vertical offset needs the
        // bounds.
        const float textSize = 220;
        var bounds = new SKRect();
        using (var probe = new SKPaint { Typeface = tf, TextSize = textSize, IsAntialias = true })
            probe.MeasureText("S", ref bounds);
        float cx = size / 2f;
        float baseY = size / 2f - bounds.MidY;   // bounds are baseline-relative → centre vertically

        void DrawS(float dx, float dy, SKColor c)
        {
            using var p = new SKPaint
            {
                Typeface = tf, TextSize = textSize, IsAntialias = true,
                TextAlign = SKTextAlign.Center, Color = c, BlendMode = SKBlendMode.Screen,
            };
            canvas.DrawText("S", cx + dx, baseY + dy, p);
        }
        // Offsets mirror sholto-icon.svg: blue back (up-left), green anchor, red front (down-right).
        DrawS(-12, 8, ToSk(theme.Stems.Drums));   // drums – blue
        DrawS(0, 0, ToSk(theme.Stems.Vocals));     // vocals – green
        DrawS(12, -8, ToSk(theme.Stems.Instrumental));   // instrumental – red

        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        var ms = new MemoryStream();
        data.SaveTo(ms);
        ms.Position = 0;
        return new WindowIcon(new Bitmap(ms));
    }

    private static SKColor ToSk(Color c) => new SKColor(c.R, c.G, c.B, c.A);

    private static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    private static SolidColorBrush Solid(Color c) => new SolidColorBrush(c);

    /// <summary>
    /// Write the theme's colors into Window.Resources keyed under "Sholto…" names.
    /// Every UI element that needs a themed color references these via
    /// {DynamicResource Sholto…}, so the references re-evaluate without going
    /// through visual-tree traversal (which goes stale under Fluent's hover/menu states).
    /// </summary>
    private void ApplyThemeToResources(SholtoTheme theme)
    {
        Resources["SholtoBgDeep"]        = theme.BgDeep;
        Resources["SholtoSurface"]       = theme.Surface;
        Resources["SholtoSurfaceRaised"] = theme.SurfaceRaised;
        Resources["SholtoBorder"]        = theme.Border;
        Resources["SholtoPrimary"]       = theme.Primary;
        Resources["SholtoAccent"]        = theme.Accent;
        Resources["SholtoAccentBg"]      = theme.AccentBg;
        Resources["SholtoMint"]          = theme.Mint;
        Resources["SholtoTextBright"]    = theme.TextBright;
        Resources["SholtoTextMuted"]     = theme.TextMuted;
        // Foreground drawn on top of Camelot key chips. Themes pick this once so
        // dark/light text stays legible against their tuned chip palette.
        Resources["SholtoChipForeground"] = theme.CamelotPalette.OnChipForeground;
        Resources["SholtoMinimapPalette"] = theme.Minimap;
        Resources["SholtoWaveformPalette"] = theme.Waveform;
        Resources["SholtoTextBrightColor"] = ((SolidColorBrush)theme.TextBright).Color;

        // Stems (window icon, watermark, link icon).
        Resources["SholtoStemDrums"]        = Solid(theme.Stems.Drums);
        Resources["SholtoStemVocals"]       = Solid(theme.Stems.Vocals);
        Resources["SholtoStemInstrumental"] = Solid(theme.Stems.Instrumental);

        // Status. Tint alphas are the ones the status pill used before theming.
        Resources["SholtoStatusOk"]        = Solid(theme.Status.Ok);
        Resources["SholtoStatusWarn"]      = Solid(theme.Status.Warn);
        Resources["SholtoStatusError"]     = Solid(theme.Status.Error);
        Resources["SholtoStatusAttention"] = Solid(theme.Status.Attention);
        Resources["SholtoStatusOkTint"]    = Solid(WithAlpha(theme.Status.Ok, 0x1F));
        Resources["SholtoStatusWarnTint"]  = Solid(WithAlpha(theme.Status.Warn, 0x33));
        Resources["SholtoStatusErrorTint"] = Solid(WithAlpha(theme.Status.Error, 0x33));
        // "Analysis failed" marker in the track list; alias of Attention.
        Resources["SholtoWarning"] = Solid(theme.Status.Attention);

        Resources["SholtoMute"]        = Solid(theme.Mute);
        Resources["SholtoScrim"]       = Solid(theme.Scrim);
        Resources["SholtoShadow"]      = Solid(theme.Shadow);
        Resources["SholtoShadowColor"] = theme.Shadow;
        Resources["SholtoDeckShadow"]  = new BoxShadows(new BoxShadow
            { OffsetX = 0, OffsetY = 6, Blur = 20, Spread = 0, Color = WithAlpha(theme.Shadow, 0xA0) });
        // "This key mixes" glow on eligible Camelot key chips.
        Resources["SholtoKeyChipGlow"] = new BoxShadows(new BoxShadow
            { OffsetX = 0, OffsetY = 0, Blur = 6, Spread = 1, Color = WithAlpha(((SolidColorBrush)theme.TextBright).Color, 0xB0) });
        Resources["SholtoIconPlate"] = Solid(theme.IconPlate);

        // Tag editor chips + track-list tag indicator. The unprefixed keys are kept
        // because TagEditorView / the track list reference them.
        Resources["SholtoTagChipBackground"]     = Resources["TagChipBackground"]     = Solid(theme.Tags.ChipBg);
        Resources["SholtoTagChipForeground"]     = Resources["TagChipForeground"]     = Solid(theme.Tags.ChipFg);
        Resources["SholtoTagIndicatorBackground"] = Resources["TagIndicatorBackground"] = Solid(theme.Tags.IndicatorBg);
        Resources["SholtoTagIndicatorForeground"] = Resources["TagIndicatorForeground"] = Solid(theme.Tags.IndicatorFg);

        Resources["SholtoFaceplateRest"]     = Solid(theme.Faceplate.Rest);
        Resources["SholtoFaceplateHover"]    = Solid(theme.Faceplate.Hover);
        Resources["SholtoFaceplateSelected"] = Solid(theme.Faceplate.Selected);
        Resources["SholtoFaceplateGlow"]     = Solid(theme.Faceplate.Glow);
    }

    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
            e.Handled = true;
            return;
        }

        if (DataContext is not MainViewModel vm) return;
        bool shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;

        // Tag editor open → let it own input. The InputBox handles
        // Tab/Enter/Esc/Backspace/Up/Down via its own KeyDown; we only need to
        // suppress global shortcuts (space, 1, 2, P, etc.) from firing
        // underneath when the user is typing a tag. Esc is a backstop in case
        // focus isn't actually on the InputBox yet.
        if (vm.IsTagEditorOpen)
        {
            // Own navigation/commit here so it works regardless of focus — the tag
            // editor is shown by toggling a parent Panel's IsVisible, so the InputBox
            // doesn't reliably receive focus and can't be relied on to catch keys.
            // Letters fall through to the focused InputBox for typing.
            var te = vm.TagEditor;
            if (te is not null)
            {
                switch (e.Key)
                {
                    case Key.Escape: te.Close();                    e.Handled = true; break;
                    case Key.Enter:  _ = te.CommitAndCloseAsync();  e.Handled = true; break;
                    case Key.Tab:    _ = te.CommitAsync();          e.Handled = true; break;
                    case Key.Down:   te.MoveSuggestion(+1);         e.Handled = true; break;
                    case Key.Up:     te.MoveSuggestion(-1);         e.Handled = true; break;
                    case Key.Back when string.IsNullOrEmpty(te.Input):
                                     _ = te.RemoveLastChipAsync();  e.Handled = true; break;
                }
            }
            return;
        }

        // Controller guide open → swallow the app's own shortcuts, same isolation as
        // the tag editor above (Space would otherwise open search underneath, 1 / 2
        // would load decks). Esc is the only key it answers to; the drawing itself
        // handles clicks via pointer events, not key events.
        if (vm.IsFaceplateOpen)
        {
            if (e.Key == Key.Escape) { vm.IsFaceplateOpen = false; e.Handled = true; }
            return;
        }

        // Crate picker owns input via its TextBox; Esc is a global backstop.
        if (vm.IsCratePickerOpen)
        {
            if (e.Key == Key.Escape) { vm.CratePicker?.Close(); e.Handled = true; }
            return;
        }

        // System report (opened by the amber status dot). Read-only; Esc is its
        // only key, same as the controller guide.
        if (vm.IsSystemReportOpen)
        {
            if (e.Key == Key.Escape) { vm.CloseSystemReport(); e.Handled = true; }
            return;
        }

        // Enter-mode action menu: arrows move + Enter fires, plus direct shortcuts
        // (C crate, T tag), Esc closes.
        if (vm.IsTrackActionsOpen)
        {
            switch (e.Key)
            {
                case Key.Up:     vm.TrackActions.Move(-1); e.Handled = true; break;
                case Key.Down:   vm.TrackActions.Move(+1); e.Handled = true; break;
                case Key.Enter:  vm.TrackActions.Commit();  e.Handled = true; break;
                case Key.Escape: vm.TrackActions.Close();   e.Handled = true; break;
                case Key.C: vm.TrackActions.Invoke(TrackActionKind.AddToCrate); e.Handled = true; break;
                case Key.T: vm.TrackActions.Invoke(TrackActionKind.Tag); e.Handled = true; break;
            }
            return;
        }

        // Same isolation for the search overlay.
        if (vm.IsSearchOpen) return;

        // Any other focused text input (current or future) gets full keyboard
        // ownership — global shortcuts skip when a TextBox is focused. Without
        // this, typing in any input would still trigger spacebar=search and
        // 1/2=load-deck via our Tunnel+handledEventsToo handler registration.
        if (FocusManager?.GetFocusedElement() is TextBox) return;

        // Spacebar opens search (from anywhere outside an input).
        if (e.Key == Key.Space)
        {
            vm.IsSearchOpen = true;
            e.Handled = true;
            return;
        }

        // Enter on a highlighted library row → the track action menu (Add to crate /
        // Tag). Replaces the old T-to-tag shortcut.
        if (e.Key == Key.Enter)
        {
            var row = vm.SelectedTrackRow;
            if (row is not null)
            {
                vm.OpenTrackActions(row);
                e.Handled = true;
                return;
            }
        }

        // Keys that are gestures (1/2 load, P play, M marker, G grid tool) are
        // recognised and dispatched by whoever subscribes to KeyPressed; the handler
        // sets Handled synchronously.
        KeyPressed?.Invoke(e);
        if (e.Handled) return;

        switch (e.Key)
        {
            case Key.Escape:
                if (vm.Deck1.EditOpen || vm.Deck2.EditOpen)
                {
                    vm.Deck1.CloseEditor();
                    vm.Deck2.CloseEditor();
                    e.Handled = true;
                    return;
                }
                // Otherwise Escape clears an active crate/tag filter, returning the
                // library to the full "All Songs" view.
                if (vm.ActiveFilter is not null)
                {
                    vm.ClearFilter();
                    e.Handled = true;
                    return;
                }
                break;
        }

        switch (e.Key)
        {
            // Beatgrid editing — only while a tune editor is open; each key is a
            // command for that deck, and the App saves the result to the DB.
            //   ← / →          phase alignment, FINE  (±10 ms)
            //   Shift + ← / →  phase alignment, COARSE (±1 beat)
            //   ↑ / ↓          BPM width, COARSE (±0.1)
            //   Shift + ↑ / ↓  BPM width, FINE   (±0.01)
            // Workflow: ↑/↓ to fix spacing drift first, then ←/→ to align.
            case Key.Left:
            case Key.Right:
            {
                // Phase nudge — ONLY while the Grid tool is open on a deck.
                // Outside edit mode these keys do nothing (grid stays locked).
                if (vm.EditingDeck is { } target)
                {
                    int sign = e.Key == Key.Left ? -1 : +1;
                    if (shift) target.NudgeGrid(sign);   // ±1 beat
                    else       target.NudgeGridFine(sign * 0.010);
                    e.Handled = true;
                }
                break;
            }
            case Key.Up:
            case Key.Down:
            {
                // Tempo — ONLY while the tune editor is open on the target deck;
                // otherwise ↑/↓ move the track-list selection (up = previous row).
                // Shift = whole-BPM steps, plain = fine (0.1).
                if (vm.EditingDeck is { } target)
                {
                    int sign = e.Key == Key.Up ? +1 : -1;   // up = faster BPM
                    double delta = shift ? 1.0 : 0.1;
                    target.AdjustBpm(sign * delta);
                }
                else
                {
                    vm.SelectTrack(vm.SelectedTrackIndex + (e.Key == Key.Up ? -1 : +1));
                }
                e.Handled = true;
                break;
            }
        }
    }

    private void OnTrackSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Selection only — no automatic load. User presses 1/2 (or FLX-4 LOAD 1/2)
        // to put the highlighted track on a deck.
        if (e.AddedItems is { Count: > 0 } && e.AddedItems[0] is TrackRow row)
            Console.WriteLine($"[Track] selected {row.Title}");
    }

    // Double-click a library row → re-run analysis on it. The single click already
    // selected the row; the VM sends the request and the headless track loader runs
    // the same decode + re-analyze path as the browse-knob long-press.
    private void OnTrackDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.RequestReanalyzeSelected();
    }

    private void OnTagIndicatorPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (sender is Avalonia.Controls.Control { DataContext: TrackRow row })
        {
            e.Handled = true;
            _ = vm.OpenTagEditorAsync(row);
        }
    }

    /// <summary>The controller guide's only entry point. Toggles: pressing it again
    /// while the guide is open closes it, same as Esc or the overlay's own close
    /// button.</summary>
    private void OnFaceplateButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        vm.IsFaceplateOpen = !vm.IsFaceplateOpen;
    }

    private void OnOutputDeviceClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.RequestChangeOutputDevice();
    }

    /// <summary>Click on the top-bar status dot. Only the amber (degraded) dot does
    /// anything — <see cref="MainViewModel.OpenSystemReport"/> no-ops on green and on
    /// red, so a healthy dot is not a hidden button and a dropped controller keeps
    /// pointing at the USB rather than at an install list.</summary>
    private void OnStatusDotPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        vm.OpenSystemReport();
        if (vm.IsSystemReportOpen) e.Handled = true;
    }

    private void OnMusicFolderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.RequestChangeMusicFolder();
    }


    /// <summary>Builds the Theme submenu from <see cref="IThemeCatalog.All"/> (order =
    /// themes.manifest) so adding/removing a theme JSON never needs a XAML or
    /// code-behind edit.</summary>
    private void BuildThemesMenu()
    {
        ThemesMenu.Items.Clear();
        foreach (var theme in _themeCatalog.All)
        {
            var item = new MenuItem { Header = theme.Name, Icon = ThemeSwatch(theme) };
            var captured = theme;
            item.Click += (_, _) => SetTheme(captured);
            ThemesMenu.Items.Add(item);
        }
    }

    /// <summary>Four 6×14 chips — background, primary, accent, mint — with a 1 px
    /// border so a dark theme's chips still read against the menu.</summary>
    private static Control ThemeSwatch(SholtoTheme t)
    {
        var strip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 1 };
        foreach (var brush in new[] { t.BgDeep, t.Primary, t.Accent, t.Mint })
            strip.Children.Add(new Border { Width = 6, Height = 14, Background = brush, BorderBrush = t.TextMuted, BorderThickness = new Thickness(0.5) });
        return strip;
    }

    private void SetTheme(SholtoTheme theme)
    {
        if (DataContext is MainViewModel vm) vm.Theme = theme;
        ApplyThemeToResources(theme);
    }
}
