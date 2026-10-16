# Contributing

## Setup

1. Install the .NET 8 SDK and MySQL Server 8 or newer.
2. Create a dedicated local MySQL user and database.
3. Copy appsettings.example.json to appsettings.Local.json.
4. Update the local connection string.
5. Run dotnet restore followed by dotnet run --project src/SuperstarReminder.

Never commit appsettings.Local.json, local audio, character source media, credentials, or generated build output.

## Change guidelines

- Keep domain scheduling logic independent of Avalonia and native OS APIs.
- Implement Windows/macOS integration behind the platform service interfaces.
- Add tests for schedule, snooze, active-hours, and duplicate-occurrence behavior.
- Validate changes on both Windows and macOS before merging platform-facing work.
- Keep realistic character assets separate from code and document their source and licensing.
