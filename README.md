# Superstar Reminder

An animated desktop reminder application for **Windows and macOS**, built with C#/.NET 8, Avalonia, and a local MySQL database.

A Jailer-inspired Superstar Rajinikanth character enters your primary display, presents a reminder, and reacts to **Yes**, **No**, and **Snooze**. The bundled cutouts are newly generated images. They are not film clips or an endorsement by the performer.

## Run it

See [setup instructions](docs/SETUP.md) for local MySQL provisioning and the first-run connection screen.

On Windows, download/install the SDK and publish the app on **D:**:

```powershell
powershell -File .\scripts\setup-windows.ps1
```

Default output: `D:\SuperstarReminder\app\SuperstarReminder.exe`. This is a self-contained folder, so the published application does not require a separately installed .NET runtime. MySQL remains a separate local prerequisite.

For development on either OS:

```sh
dotnet restore SuperstarReminder.sln
dotnet run --project src/SuperstarReminder
```

The first-run **Settings** dialog tests your database login. There is no in-memory fallback and no sample data shown as real reminders.

## Implemented

- Create, edit, enable/disable, and soft-delete custom reminders.
- One-time, daily, selected-weekday, and interval schedules with optional active hours, including midnight-crossing ranges.
- Saved time zones, fixed interval cadence, DST gap/overlap handling, and persisted next-due times.
- Yes acknowledges; No skips; Snooze offers 5/10/20/30 minutes and can fire outside active hours. Normal recurring schedules stay unchanged.
- Thirty-second unanswered timeout, one character at a time, preview without history writes.
- Separate transparent character and interactive banner windows, primary-monitor work-area placement, adjustable size, and both bottom corners.
- Happy reaction and long exit on Yes; short exit for No/Snooze/timeout.
- Manifest-based character loading with twelve-frame idle/happy/walk/run loops. A compact sample package exercises the same loader.
- Silent default, optional local WAV/MP3 arrival audio, and managed media copies.
- Tray/menu-bar controls, close-to-tray, pause/resume, optional launch at login.
- MySQL migrations, parameterized queries, transactional outcomes/snoozes, duplicate occurrence keys, and title-preserving response history.
- A local response recovery journal retries uncommitted responses after service outages or restarts.
- Windows DPAPI and macOS Keychain protect the saved login. No credentials are stored in the repository.
- Best-effort native lock/fullscreen deferral and one consolidated catch-up notification with an in-app summary/history fallback.

## Validation and release status

This is an implementation under validation, **not a signed production release**. Build, scheduling, UI-smoke and MySQL integration checks are provided. See [validation notes](docs/VALIDATION.md) for what was actually run in the development workspace.

Native click-through/focus, fullscreen detection, macOS Spaces, notifications, audio, startup, DPI/Retina and sleep/lock recovery still need real Windows and macOS acceptance testing. Local notification availability depends on OS permissions and application identity; fallback status/history remains available. The generated short sprite loops need motion review. macOS signing/notarization and graphical installers are not included.

```sh
dotnet build SuperstarReminder.sln -c Release
dotnet run --project tests/SuperstarReminder.Tests
dotnet run --project tests/SuperstarReminder.UiTests
# Requires a disposable database ending in _test:
dotnet run --project tests/SuperstarReminder.DatabaseTests
```

GitHub Actions builds Windows x64, macOS Apple Silicon, and macOS Intel folders, runs checks, and uploads application artifacts. A separate job exercises MySQL 8.4. These workflows run only once the repository is published.

## Structure

- `src/SuperstarReminder` — dashboard, editor, settings, overlay, scheduler and adapters
- `db` — versioned MySQL migrations
- `tests` — scheduling, headless UI and database checks
- `scripts` — D: drive setup and authenticated GitHub publishing helpers
- `docs` — setup, character contract, validation and known limitations
- [V1-plan.md](V1-plan.md) — product plan
- [SECURITY.md](SECURITY.md) and [CONTRIBUTING.md](CONTRIBUTING.md)

To publish the prepared local repository using your own authenticated GitHub CLI, run `powershell -File scripts/publish-github.ps1`. The script creates the public `puneeee/superstar-reminder` repository if absent, checks the destination, and pushes main without force-pushing.
