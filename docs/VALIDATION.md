# Validation record — 16 October 2026

## Completed in the Linux cloud workspace

- Official .NET SDK **8.0.426**, runtime **8.0.32**, installed in `/opt/superstar-dotnet`.
- NuGet restore completed without audit warnings.
- Application Release build: **0 warnings, 0 errors**.
- **25 scheduling checks passed**: interval anchoring, disabled/one-time schedules, weekdays, active-hour boundaries, overnight ranges, invalid inputs, DST gaps and ambiguous fall-back times.
- **17 headless UI/overlay checks passed**: dashboard AXAML, no fake rows, editor/settings, character packages and frame loading, path-traversal rejection, Yes/No/Snooze, selected snooze duration, real 30-second timeout, and cleanup after each response.
- Actual UI screenshots rendered using Avalonia/Skia, not an image-generation mockup.
- Windows x64, macOS Apple Silicon, and macOS Intel self-contained application folders were cross-published successfully. The produced app hosts were verified as PE x64, Mach-O arm64, and Mach-O x64 respectively.
- Database integration check executable builds successfully. A temporary MySQL 8.0.46 data directory initialized successfully, but this workspace disallows UNIX socket creation, preventing the MySQL server from starting. Live database checks are therefore delegated to the GitHub Actions MySQL job; a passing local database test is not claimed.

The SDK needed a temporary process-ID mapping shim to run build tooling in this cloud sandbox. That shim lives outside the repository, is not part of the app, and is not needed on ordinary Windows/macOS development machines.

## Still requires platform acceptance testing

This workspace is Linux, with no access to the user's Windows D: drive or a real Mac desktop. Cross-publishing does not prove native desktop behavior.

- Real Windows/macOS install and MySQL connection/migration/create/edit/restart flows.
- Windows taskbar and macOS Dock/menu bar, primary-display changes, mixed DPI/Retina, and banner placement.
- Native click-through and no-focus-stealing behavior; macOS Spaces/fullscreen transitions.
- Lock/sleep/fullscreen deferral and notification delivery under OS notification permissions/Focus settings.
- WAV/MP3 playback and cancellation, launch at login, Keychain and DPAPI behavior.
- Animation motion quality on real display frame rates.
- Signed Windows installer and signed/notarized macOS application bundles are not provided yet.

The code includes best-effort platform adapters and an in-app catch-up fallback. Do not describe this build as having passed these native acceptance criteria.
