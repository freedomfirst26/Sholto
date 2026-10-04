using System;
using Avalonia.Controls;

namespace Sholto.Interface.MainUI.Theming;

/// <summary>Builds the system tray icon for the main window.</summary>
public interface ITrayFactory
{
    /// <summary>Shows the brand icon in the tray with a Show / Quit menu. Never throws:
    /// if the platform has no tray (on Linux, no StatusNotifierItem watcher), logs one
    /// line and returns a no-op handle. Dispose the result on shutdown.</summary>
    IDisposable Create(Window window, SholtoTheme theme);
}
