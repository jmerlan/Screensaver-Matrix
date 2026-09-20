# Security Policy

## Supported versions

Only the latest release receives fixes.

## Reporting a vulnerability

Please **do not** open a public issue for security problems. Instead, report them privately with
GitHub's [private vulnerability reporting](../../security/advisories/new) (the **Security** tab →
**Report a vulnerability**).

Include the affected version, steps to reproduce, and the impact you expect. You should get a response
within a week. Once a fix is released, you'll be credited in the release notes unless you'd prefer not
to be.

## What the screensaver does on your system

For transparency:

- **Installs to** `C:\Windows\System32\Matrix.scr` (installer, with administrator rights).
- **Writes the registry** only in `HKCU\Software\MatrixScreensaver` (its settings), plus
  `HKCU\Control Panel\Desktop` → `SCRNSAVE.EXE` / `ScreenSaveActive` if you choose to make it your screen
  saver.
- **Reads files** only from the hidden-image file or folder you select.
- **Makes no network connections**, collects no telemetry, and runs no background services.

Release binaries are built by GitHub Actions from the tagged source, and SHA-256 checksums are published
with each release.
