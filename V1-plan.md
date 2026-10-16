# Superstar Reminder — V1 Plan

Status: Implementation authorized on 16 October 2026. Initial functional implementation is under validation; see docs/VALIDATION.md.
Date: 15 October 2026
Working title: Superstar Reminder (final name can change).

## 1. Product

A Windows and macOS desktop reminder application inspired by Wise Reminder's scheduling concept and the supplied desktop-character video. Users create custom reminders. A realistic Rajinikanth cutout inspired by his appearance in Jailer enters the desktop, displays a speech banner, and reacts to Yes, No, or Snooze.

Windows and macOS are both required V1 platforms. Rajinikanth is the first character; the architecture supports additional character packages later.

## 2. Confirmed requirements

| Area | V1 behavior |
| --- | --- |
| Reminders | Custom title and message; create, edit, enable/disable, delete |
| Schedules | One-time, daily, selected weekdays, and every X minutes |
| Active hours | Optional user-defined active hours |
| Actions | Yes, No, and Snooze with a duration dropdown |
| Snooze choices | Proposed presets: 5, 10, 20, and 30 minutes |
| Snooze outside active hours | Explicit snooze still fires outside active hours, subject to lock/fullscreen deferral |
| Unanswered | Leave after 30 seconds; record unanswered |
| Character | Realistic animated Rajinikanth cutout inspired by Jailer |
| Size | Small default; adjustable character height |
| Placement | Primary monitor only; bottom-right default; selectable bottom-left |
| Audio | Silent default; optional locally selected audio on character arrival |
| Background | Windows system tray / macOS menu bar; closing settings keeps reminders running |
| Startup | Optional launch at user login on either OS |
| Storage | MySQL from V1; simple response history |
| Fullscreen/lock/sleep | Defer presentation; present one consolidated catch-up notice after returning |

## 3. Character interaction

1. A due reminder enters the coordinator's queue.
2. When presentation is allowed, the character enters from the configured nearby edge.
3. It stops inside the primary display's usable work area, clear of the taskbar or Dock, faces the user, and displays the reminder banner.
4. Optional local audio plays once on arrival. No response dialogue is required in V1.
5. The 30-second response timer begins when the buttons are ready.
6. The first valid action wins; disable further actions while exiting.
7. Persist the outcome and run the exit animation.

| Placement | Yes | No / Snooze / timeout |
| --- | --- | --- |
| Bottom-right | Happy reaction, then run left across the monitor | Walk right to the nearby edge |
| Bottom-left | Happy reaction, then run right across the monitor | Walk left to the nearby edge |

Yes acknowledges the occurrence; it does not verify that the user performed the activity. No skips the occurrence. Snooze postpones that occurrence. Timeout records unanswered and uses the short exit (proposed default).

For recurring reminders, Yes, No, and timeout preserve the regular schedule. Snooze does not move the recurring schedule. Proposed collision policy: merge a snoozed occurrence with a regular occurrence due at the same time instead of presenting duplicates.

## 4. Screens

### Reminder list and editor

- List reminders with next due time and enabled state.
- Editor: title, message, schedule type, schedule values, optional active hours, optional audio, and save/cancel.
- Preview button to test the selected character and banner without recording an actual response.
- For minute intervals, proposed default is a fixed cadence anchored to the saved start time; skip occurrences outside active hours.
- Active-hour ranges can cross midnight. One-time reminders outside active hours require a clear editor validation message rather than silently changing the requested time.

### Settings

- Character picker, initially containing Rajinikanth.
- Character height slider with preview and small default.
- Bottom-left/bottom-right placement on primary monitor.
- Optional arrival audio: local file picker, playback preview, volume, remove.
- Optional launch at user login on either OS; pause/resume reminders.
- MySQL connection setup and connection test.

### History

- Reminder title, scheduled time, response time, outcome, and snooze duration.
- Filter by reminder and date.

### Tray / menu-bar menu

Open reminders, pause/resume, settings, and explicit Quit. The settings close button does not quit the app.

## 5. Technology choices

| Technology | Purpose |
| --- | --- |
| C# and a supported .NET LTS release | Application and scheduling logic |
| Avalonia UI and AXAML | Shared editor, settings, banner, and animation player for Windows and macOS |
| MVVM | Separate screen state and commands from presentation |
| Win32 / macOS AppKit and system API adapters | Focus, click-through, monitor/work-area detection, session events, and startup |
| MySQL Server, local service on each OS | Persistent reminders, occurrences, preferences, and history |
| MySqlConnector + Dapper | Parameterized database access from C# |
| Transparent PNG frames or sprite sheets | Realistic cutout animations with predictable transparency |
| JSON character manifest | Animation definitions, timing, anchors, and character metadata |
| Windows App SDK / macOS UserNotifications adapters | Native local notifications on each OS, subject to integration validation |
| Git and GitHub | Documentation and source version history |

Exact runtime, database, connector, and notification SDK versions will be pinned after compatibility validation during implementation.

## 6. Architecture

A modular desktop application with no hosted application backend required for V1.

| Module | Responsibility |
| --- | --- |
| Domain | Reminder rules, schedules, occurrences, response meanings |
| Scheduler | Calculate next due times and recover persisted schedules |
| Coordinator | Queue, deferral, snooze, duplicate prevention, catch-up |
| Presentation | Settings/editor, overlay, banner, character state machine |
| Character catalog/player | Load packages; play animations and movement |
| Infrastructure | MySQL repositories, audio, notification adapters, Windows and macOS integration |

The scheduler emits an occurrence; the coordinator selects when and how to present it. UI and notification actions use the same response service. Database operations remain asynchronous so the animation thread is not blocked.

Core reminder logic must not depend on Avalonia, native OS APIs, or a specific character. Shared interfaces isolate platform behavior: IOverlayHost, INotificationService, IAudioPlayer, ISessionMonitor, IAutoStartService, and ISecretStore. Separate Windows and macOS adapters implement these contracts. UI, schedules, MySQL access, and character assets are shared.

## 7. MySQL setup and reliability

MySQL is required from the first version; SQLite is not part of the plan. Proposed deployment is a local MySQL service on the user's Windows PC or Mac, so reminders work without an internet connection while that service is running.

- Provide prerequisite instructions and a first-run connection test.
- Use a dedicated app database and restricted application account; do not run the app as database root.
- Protect the connection secret using Windows-protected user storage on Windows and Keychain on macOS; exclude credentials from Git and logs.
- Apply versioned schema migrations.
- Persist due times, snoozes, and outcomes transactionally.
- On service failure, show a clear degraded-status message and retry. Do not claim a response was saved if persistence failed.
- Reconcile due occurrences when connectivity returns; avoid a burst of old reminders.
- Avoid a continuously polling database loop; compute the next due time and re-evaluate after relevant changes.

MySQL adds installation and background-service overhead. Packaging and service provisioning need an explicit implementation step.

## 8. Proposed data model

| Entity | Main fields |
| --- | --- |
| Reminder | id, title, message, enabled, schedule_type, schedule_config, timezone_id, active_hours, audio_asset_id, next_due_at, timestamps |
| Occurrence | id, reminder_id, scheduled_at, original_due_at, state, snoozed_until, presented_at, responded_at |
| Response event | id, occurrence_id, action, event_time, snooze_minutes |
| Preferences | corner, character_id, character_height, startup_enabled, audio_volume, pause_state |
| Media asset | id, local_managed_path, media_type, display_name |
| Schema version | version, applied_at |

Store event instants in UTC and preserve the reminder's time zone for local schedules. Use a unique occurrence key to prevent duplicate scheduling. Editing/deleting a reminder must cancel or reconcile pending occurrences; historical events remain identifiable through a stored title snapshot or equivalent strategy.

## 9. Character package contract

Each package contains metadata and transparent animation assets. Required actions:

- Enter/walk toward reminder position.
- Idle facing the user.
- Happy reaction.
- Run left and run right.
- Walk left and walk right.

The manifest defines ID, display name, preview, frame timing, looping, foot anchor, banner anchor, asset paths, and supported actions. Validate packages and provide a safe idle fallback for missing optional animations. Prefer dedicated directional assets where mirroring would reverse clothing details or text.

Use consistent frame dimensions, character scale, and foot placement. Move the walking/running loop across the desktop while changing poses through the state machine. Unload or stop animation work while hidden.

No character assets are produced or extracted as part of this documentation task. Asset production and quality review are a separate development milestone.

## 10. Desktop behavior

- Use the primary monitor's usable work area and account for Windows taskbar, macOS Dock/menu bar, and DPI/Retina scaling.
- Keep the banner inside the visible work area and character feet clear of the taskbar or Dock.
- Do not steal keyboard focus when a reminder arrives.
- Target: empty overlay areas pass clicks through; buttons stay interactive. Avalonia transparency alone does not provide per-pixel click-through. Validate native input-region/hit-testing approaches on each OS; if needed use a compact click-through character window plus a separate interactive banner.
- Present one character at a time; queue simultaneous reminders.
- Pause presentation during detected fullscreen use or session lock. Detection must be tested across representative apps on both OSes, including macOS Spaces and fullscreen transitions. Defer rather than force the overlay into a fullscreen Space.
- On resume/unlock/fullscreen exit, summarize missed reminders rather than animating every overdue occurrence.
- A single app instance owns scheduling and presentation.
- Quitting the app stops character reminders; closing settings does not.

## 11. Native desktop notifications — interpretation for review

The user's phrase 'push notification' is interpreted here as a locally generated native notification on Windows or macOS. True server-to-device push would require additional infrastructure and is not assumed.

Proposed use: one catch-up notification after deferred reminders, opening the app's pending-reminder view when clicked. The animated overlay remains the normal reminder experience. Avoid two simultaneous alerts for the same occurrence. Respect each OS's notification permissions, Focus/Do Not Disturb, and suppression settings; keep an in-app catch-up summary if a toast cannot be displayed.

Implemented as local catch-up notifications with in-app history/status fallback; native delivery still requires OS acceptance testing.

## 12. Audio

- Optional local file selection; silent default.
- Play once when the character arrives and stop on dismissal/exit.
- Proposed V1 formats: WAV and MP3, validated with the chosen playback implementation.
- Copy selected audio into the application's managed media folder so moving the original does not break reminders.
- Missing or invalid audio never prevents the visual reminder.
- No online audio download or voice generation in V1.

## 13. Delivery milestones after approval

1. Confirm document and GitHub destination; push documentation only.
2. Separately obtain approval to begin implementation.
3. Validate transparency, focus/click-through, work-area placement, notifications, and audio on actual Windows and macOS machines before full feature implementation.
4. Set up MySQL schema, connection UI, migrations, and repositories.
5. Build reminder editor, scheduler, occurrences, snooze, and history.
6. Produce and review Rajinikanth animation assets; connect the character player.
7. Add audio, tray/startup, deferral, and catch-up handling.
8. Verify both platforms and prepare Windows and macOS packages with MySQL prerequisite instructions; plan macOS signing/notarization for distribution.

## 14. Acceptance criteria

- All acceptance criteria must pass on both Windows and macOS.
- Custom reminders survive restart and fire according to their saved schedules.
- Active hours, midnight-crossing ranges, sleep/resume, and clock/time-zone changes are handled consistently.
- Character appears only on the primary monitor at the selected corner and adjustable small size.
- Yes plays happiness and the long run; No/Snooze use the nearest short exit.
- Snooze postpones the occurrence, including outside active hours; duplicate actions and collisions do not create duplicate reminders.
- Unanswered reminders exit after 30 seconds of actionable display.
- Fullscreen/lock/sleep deferral produces one catch-up notice after returning.
- Audio is optional, local, silent by default, and plays only on arrival.
- Closing settings keeps scheduling active; Quit stops the app.
- MySQL outage/recovery is visible and does not silently lose acknowledged data.
- A second sample character package can be loaded through configuration without modifying scheduler logic.

## 15. Cross-platform release requirements

- Target Windows x64 and macOS Apple Silicon and Intel, within the supported releases of the chosen runtime/UI versions. Pin minimum OS versions before implementation.
- Produce separate packages from shared source; use a macOS build/signing environment and real Mac validation for release readiness.
- Test display changes, primary-display reassignment, Retina/DPI scaling, taskbar/Dock position, sleep/resume, lock, fullscreen, and macOS Spaces.
- Test local WAV/MP3 playback, credential protection, launch at login, and native notification activation separately on each OS.
- Use OS-appropriate per-user data folders and portable paths.
- Install/configure MySQL as a separate local prerequisite on each OS; bind the database to local access by default.
- Windows-only delivery does not satisfy V1. Both platforms must pass core flows and overlay checks.

## 16. Publishing authorization

The user authorized a public `superstar-reminder` repository, publishing the plan and implementation, and installing required development prerequisites. Repository creation currently requires authentication in a creation-capable GitHub session. No remote repository or successful push is assumed until verified.

## 17. Technical references

- Avalonia: https://docs.avaloniaui.net/
- Avalonia window behavior: https://docs.avaloniaui.net/docs/how-to/window-how-to
- Avalonia macOS: https://docs.avaloniaui.net/docs/platform-specific-guides/macos
- Avalonia tray icons: https://docs.avaloniaui.net/controls/navigation/trayicon
- MySQL Connector/NET: https://dev.mysql.com/doc/connector-net/en/
- Windows app notifications for .NET: https://learn.microsoft.com/windows/apps/develop/notifications/app-notifications/app-notifications-dotnet

