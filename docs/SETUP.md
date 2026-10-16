# Run Superstar Reminder

## Windows (downloads and SDK on D:)

Run PowerShell from the repository:

```powershell
powershell -File .\scripts\setup-windows.ps1
```

This process downloads the official Microsoft SDK installer into `D:\SuperstarReminder\downloads`, installs .NET into `D:\SuperstarReminder\tools\dotnet`, uses a NuGet cache on D:, builds and checks the code, and publishes the self-contained application into `D:\SuperstarReminder\app`. It does not install anything onto another drive if D: is absent. Change `-InstallRoot` if needed. The app's ordinary preferences and protected login use the Windows per-user application data folder.

Install MySQL Community Server 8.0+ from https://dev.mysql.com/downloads/mysql/. Save its download on D: as well. The app does not need an administrator account or install a database service itself. In the MySQL installer, select a local server, bind to loopback, and use a strong administration password. Do not use your administration account in the application.

## macOS

Install the .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0 and MySQL Community Server 8.0+ from https://dev.mysql.com/downloads/mysql/. Start the MySQL service using its supported installer or service instructions. Then:

```sh
dotnet restore SuperstarReminder.sln
dotnet build SuperstarReminder.sln -c Release
dotnet run --project tests/SuperstarReminder.Tests
dotnet run --project src/SuperstarReminder
```

To publish a self-contained application, use `dotnet publish src/SuperstarReminder -c Release -r osx-arm64 --self-contained true -o artifacts/osx-arm64` (Apple Silicon), or `osx-x64` for Intel. Run `./artifacts/osx-arm64/SuperstarReminder`. A signed/notarized .app installer is not yet supplied. Launch-at-login needs a stable published executable path; do not move it after enabling startup.

## Create the MySQL database once

Connect using your MySQL administrator through the MySQL client. Replace the example password before executing this SQL:

```sql
CREATE DATABASE superstar_reminder CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'superstar_app'@'127.0.0.1' IDENTIFIED BY 'replace-with-a-strong-local-password';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, REFERENCES
ON superstar_reminder.* TO 'superstar_app'@'127.0.0.1';
```

The `CREATE`, `ALTER`, `INDEX`, and `REFERENCES` privileges support the app's versioned migrations. The server may resolve loopback to `localhost`; if login fails, check the host of the restricted user rather than switching to root. The Settings connection test reports the MySQL error number. No password is written to logs.

Launch the app, open **Settings**, enter `127.0.0.1`, port `3306`, database `superstar_reminder`, user `superstar_app`, and your password. Click **Test connection & save**. Migrations apply automatically. Windows protects the login with DPAPI; macOS uses Keychain. For development only, a process-scoped `SUPERSTAR_MYSQL_CONNECTION` environment variable overrides the saved login. It is never required for normal desktop setup.

## First reminder

1. Click **New reminder**, enter a title and message, and choose a schedule.
2. Select a future start time. Interval schedules stay anchored to that start time; recurring schedules use the reminder's saved time zone.
3. Optionally enable active hours, including ranges crossing midnight.
4. Optionally choose a local WAV/MP3 arrival clip, up to 25 MB. Audio is copied into the app's managed media folder. Default is silent.
5. Save. Use **Preview character** to see the overlay without recording history.
6. On a due reminder, choose **Yes**, **No**, or **Snooze** (5/10/20/30 minutes). Unanswered prompts time out after 30 seconds.

The dashboard closes to the tray/menu bar. Use its tray icon or menu to reopen it, pause/resume, or explicitly quit. The primary display and bottom corner are configurable in Settings. Explicit snooze can fire outside active hours. Snooze creates a pending occurrence without moving the recurring cadence.

## Build and verification

```sh
dotnet build SuperstarReminder.sln -c Release
dotnet run --project tests/SuperstarReminder.Tests
dotnet run --project tests/SuperstarReminder.UiTests
```

An optional `SUPERSTAR_TEST_SCREENSHOTS` environment variable saves actual headless UI renders from the smoke checks. These checks do not prove native fullscreen, lock, notifications, click-through, audio, or login startup behavior; those require real Windows and macOS testing.
