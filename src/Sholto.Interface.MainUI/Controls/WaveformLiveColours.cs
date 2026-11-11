using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Sholto.Data;
using SkiaSharp;

namespace Sholto.Interface.MainUI.Controls;

/// <summary>The per-frame live colours (not baked) the blit op draws with.</summary>
internal readonly record struct WaveformLiveColours(SKColor Playhead, SKColor BeatTick, SKColor Marker, SKColor VocalActive, SKColor VocalInactive, SKColor Glow, SKColor Edge);
