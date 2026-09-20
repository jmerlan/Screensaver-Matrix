# Contributing

Thanks for your interest in improving Matrix Screensaver! Bug reports, feature ideas and pull requests
are all welcome.

## Reporting bugs and requesting features

Please [open an issue](../../issues/new/choose) using the relevant template. For bugs, include:

- Windows version, monitor setup (count, resolutions, display scaling)
- Screensaver version (Settings → Apps, or the file properties of `Matrix.scr`)
- Your settings. A screenshot of the settings window is ideal.
- What happened and what you expected

## Development setup

1. Windows 10/11 with the [.NET SDK](https://dotnet.microsoft.com/download) 8 or later.
2. Clone the repository and build:
   ```powershell
   .\build.ps1
   ```
3. Run the build output directly. No installation is needed:
   ```powershell
   .\bin\Release\net48\Matrix.exe /c    # settings dialog (use "Test full screen")
   .\bin\Release\net48\Matrix.exe /s    # full screen
   ```
   Tip: run the `.exe` rather than the `.scr`. Launching a `.scr` through the shell always adds `/S`.

Any editor works. Visual Studio, VS Code with C# Dev Kit, and Rider can all open
`MatrixScreensaver.csproj`.

To build the installer as well, install [Inno Setup 6](https://jrsoftware.org/isinfo.php) and run
`.\build.ps1 -Installer`.

## Project layout

| Path | Contents |
|---|---|
| `src/` | All C# source. See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md). |
| `assets/` | Application icon and the built-in hidden images (embedded resources) |
| `tools/` | `generate-assets.py`, which draws the built-in hidden images |
| `installer/Matrix.iss` | Inno Setup script |
| `docs/` | Architecture notes and README images |
| `build.ps1` | Build script used locally and in CI |

## Guidelines

- **Target .NET Framework 4.8** so the `.scr` runs on any Windows 10/11 machine with nothing extra to
  install. Newer C# language features are fine (`LangVersion` is `latest`), but you can't use
  newer .NET APIs such as `Math.Clamp` or `Array.Fill`.
- **Keep the output a single file.** The one NuGet dependency (the `TripleZeroLabs.Os2` WPF theme) is
  embedded into the executable by the `EmbedThemeAssembly` target and loaded through
  `Program.ResolveEmbeddedAssembly`. Anything else you add must be embedded the same way, or the
  screensaver breaks when `Matrix.scr` is copied to System32 on its own.
- **Performance matters.** The render loop runs 60 times a second on every monitor. Avoid allocations
  and GDI+ calls in `MatrixRain.Update`/`Render`. Check 4K performance when changing them.
- **New settings** need all of the following: a field with default and min/max in `Settings.cs`,
  `Load`/`Save` entries (clamp on load), a control in `SettingsWindow` (including `LoadControls`), and a
  row in the README settings table. If the setting affects glyph geometry, add it to the
  `sizeChanged` check in both `MatrixView.ApplySettings` and `MatrixPreview.ApplySettings`.
- **The settings window is WPF** (built in code, no XAML) and themed with `Os2Theme.Apply`; the
  screensaver windows themselves stay WinForms. Prefer theme resource keys (`Os2ResourceKey.*`) over
  hard-coded colours.
- Match the existing code style. `.editorconfig` covers the basics.

## Testing checklist

There are no automated UI tests. Before opening a pull request, please check:

- [ ] `.\build.ps1` succeeds with no warnings
- [ ] Settings window: every control updates the live preview; **OK** saves and **Cancel** discards
- [ ] **Test full screen** covers every monitor and exits on mouse move or key press
- [ ] The Windows Screen Saver Settings thumbnail preview renders and stops when the dialog closes
- [ ] If you touched rendering: check at 100% and 150%+ display scaling, and on a 4K monitor if you
      have one

## Pull requests

1. Fork the repository and create a branch from `main`.
2. Keep each pull request focused on one change, and describe what it does and how you tested it.
   Include a screenshot for visual changes.
3. Add a line to the *Unreleased* section of [CHANGELOG.md](CHANGELOG.md).

## Releasing (maintainers)

1. Bump `<Version>` in `MatrixScreensaver.csproj` and move the *Unreleased* changelog entries under the
   new version.
2. Commit, then tag and push: `git tag v1.2.3 && git push origin v1.2.3`.
3. The **Release** GitHub Actions workflow builds `Matrix.scr` and the installer, then publishes a
   GitHub release with SHA-256 checksums.

By contributing, you agree that your contributions will be licensed under the project's
[MIT License](LICENSE).
