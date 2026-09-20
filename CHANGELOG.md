# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added
- "Clear" button next to the hidden-image path to go back to the built-in skull.

### Changed
- Settings window rebuilt in WPF and styled with the TripleZeroLabs.Os2 theme, which is embedded in
  the `.scr` so it stays a single self-contained file.
- The live preview renders into a WPF bitmap; the renderer now draws through an `IPixelSurface`
  shared with the screensaver's GDI surface.
- DPI awareness is set per mode at runtime instead of in the manifest: per-monitor for the
  screensaver, system-aware for the settings window.

## [1.0.0] - 2026-09-19

### Added
- Digital rain screensaver with mirrored katakana, digits and symbols, a bright leading character,
  fading trails and glyph shimmer.
- Settings for color (presets, custom, rainbow), speed, character size, character width, column
  spacing, stroke weight, density and trail duration.
- Optional CRT scanlines with adjustable strength.
- Hidden image mode: a built-in skull, a chosen image, or a random image from a folder fades in and
  out of the rain.
- Multi-monitor support (all monitors, or primary only) with per-monitor DPI awareness.
- Settings dialog with a live preview and a "Test full screen" button.
- Inno Setup installer that installs to System32, can set Matrix as the active screen saver, and adds
  Start menu shortcuts and an uninstaller.
