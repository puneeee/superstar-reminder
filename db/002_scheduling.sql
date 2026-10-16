ALTER TABLE reminders ADD COLUMN weekdays_json VARCHAR(64) NOT NULL DEFAULT '[]',
  ADD COLUMN next_due_at_utc DATETIME(6) NULL,
  ADD COLUMN deleted BOOLEAN NOT NULL DEFAULT FALSE;
UPDATE reminders SET next_due_at_utc = starts_at_utc;
ALTER TABLE reminder_occurrences ADD COLUMN title_snapshot VARCHAR(120) NOT NULL DEFAULT 'Reminder';
CREATE UNIQUE INDEX ux_occurrence_due ON reminder_occurrences(reminder_id, due_at_utc);
