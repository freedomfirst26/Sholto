using System;
using Avalonia;
using Avalonia.Controls;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>The tray icon is the window icon (same <see cref="IAppIconFactory"/> image).
/// Clicking it or "Show Sholto" restores and activates the window; "Quit" closes the
/// main window, the same path as its close button.</summary>
public sealed class TrayFactory(IAppIconFactory icons) : ITrayFactory
{
    private readonly IAppIconFactory _icons = icons;

    public IDisposable Create(Window window, SholtoTheme theme)
    {
        try
        {
            void Show()
            {
                if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
                window.Show();
                window.Activate();
            }

            var show = new NativeMenuItem("Show Sholto");
            show.Click += (_, _) => Show();
            var quit = new NativeMenuItem("Quit");
            quit.Click += (_, _) => window.Close();
            var menu = new NativeMenu();
            menu.Items.Add(show);
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(quit);

            var tray = new TrayIcon
            {
                Icon = _icons.Create(theme),
                ToolTipText = "Sholto",
                Menu = menu,
                IsVisible = true,
            };
            TrayIcon.SetIcons(Application.Current!, new TrayIcons { tray });
            tray.Clicked += (_, _) => Show();
            return new TrayHandle(tray);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Tray] unavailable: {e.Message}");
            return new TrayHandle(null);
        }
    }

    private sealed class TrayHandle(TrayIcon? tray) : IDisposable
    {
        private readonly TrayIcon? _tray = tray;

        public void Dispose()
        {
            if (_tray is null) return;
            try
            {
                _tray.IsVisible = false;
                _tray.Dispose();
            }
            catch (Exception)
            {
                // Shutting down; a tray that will not go away quietly is not worth a crash.
            }
        }
    }
}
