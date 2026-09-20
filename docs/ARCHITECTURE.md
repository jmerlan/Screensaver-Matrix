# Architecture

This document explains how the screensaver works internally, for contributors.

## Overview

```
Program.cs          parses /s /c /p, loads Settings, picks a mode
 ├─ ScreenSaverForm  one borderless topmost window per monitor (or a child of the preview window)
 │   └─ MatrixView   WinForms control: owns the timer, the drawing surface and the simulation
 │        ├─ MatrixRain    simulation + renderer (character grid → pixels)
 │        │    └─ GlyphAtlas  pre-rendered glyph coverage masks
 │        ├─ HiddenImage   loads/fades the hidden image, exposes a per-cell brightness map
 │        └─ DibSurface    GDI DIB section that MatrixRain writes pixels into
 └─ SettingsForm     settings dialog; hosts a MatrixView as a live preview
Settings.cs         all options, persisted to HKCU\Software\MatrixScreensaver
Native.cs           Win32 P/Invoke declarations
```

## Screensaver modes

Windows starts a `.scr` with:

- `/s`: full screen. `ScreenSaverForm.ShowFullScreen` creates one window per entry in
  `Screen.AllScreens`. Any key, click or real mouse movement closes all of them. Windows sends a
  `MouseMove` when a window appears, so movement under 8 px is ignored.
- `/p <hwnd>`: preview. The form is re-parented into the given window with `SetParent` and made a
  `WS_CHILD`. A watchdog timer exits when the parent window disappears.
- `/c[:hwnd]` or nothing: the settings dialog, optionally owned by the given window.

The app is per-monitor DPI aware (PerMonitorV2, declared in `app.manifest`). Every monitor is rendered
at native resolution, and the character size is scaled by each window's DPI (`GetDpiForWindow`).

## Rendering

Drawing thousands of glyphs per frame with GDI+ `DrawString` is far too slow at 4K, so rendering is done
by hand:

1. **GlyphAtlas** rasterizes each glyph once, as a `GraphicsPath` (optionally outlined with a pen for
   stroke weight, stretched horizontally, and mirrored for katakana). Each glyph becomes an 8-bit
   coverage mask the size of one grid cell.
2. **MatrixRain** keeps a grid of cells. Each cell has a glyph index, a brightness (0–1), a fade rate and
   a "head" flag.
3. Each frame, brightness is quantized to 32 levels plus a "head" level. A cell is redrawn only when
   its level or glyph changed. Redrawing a cell means `color × coverage` written straight into the
   `DibSurface` pixel buffer.
4. `MatrixView` then `BitBlt`s the whole surface to the window.

Colors come from a per-column palette: one shared palette, or one per column in rainbow mode. CRT
scanlines are a per-pixel-row brightness multiplier applied while writing each cell.

## Simulation

- **Drops** fall down columns at `baseSpeed × random(0.6–1.3)` rows per second. When a drop's head
  enters a row, that cell gets a random glyph, full brightness and a fade rate of `1 / trailSeconds`.
- **Spawning:** each column spawns drops at a rate derived from *density*, normalized by screen height
  so the look is the same on any monitor. A new drop may fall through an older drop's lingering trail,
  but heads keep a minimum gap and never overtake each other.
- **Shimmer:** a small number of random lit cells change glyph every frame.
- **Prewarm:** the simulation runs for a few simulated seconds before the first frame, so the screen
  starts full.

## Hidden image

`HiddenImage` turns an image into a per-cell map with values from −1 (dark) to +1 (bright):

1. The image is decoded on a background task and fitted inside the screen.
2. It is downsampled to one sample per character cell, and its contrast is stretched between the 3rd
   and 97th percentiles.
3. Cells outside the image take the value of the image's own border, with a feathered edge, so a
   subject on a dark background blends in without a visible frame.

It then runs a slow envelope: wait, fade in (10 s), hold (25 s), fade out (10 s), then a random 20–60 s
gap before the next image. `MatrixRain` applies `strength × envelope × map` in two ways:

- **Fade rate:** trails linger longer where the map is bright and fade faster where it is dark.
- **Brightness:** dark areas are dimmed by up to 75%, and bright areas are lifted slightly.

Any single frame looks like normal rain. The image only emerges when your eye averages over several
seconds.

The built-in skull (`assets/skull.png`) is an embedded resource, and an empty image path selects it.
