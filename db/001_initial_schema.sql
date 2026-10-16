CREATE TABLE IF NOT EXISTS schema_versions (version INT PRIMARY KEY, applied_at_utc DATETIME(6) NOT NULL);
CREATE TABLE IF NOT EXISTS reminders (
  id CHAR(36) PRIMARY KEY, title VARCHAR(120) NOT NULL, message VARCHAR(500) NOT NULL, enabled BOOLEAN NOT NULL DEFAULT TRUE,
  schedule_type VARCHAR(20) NOT NULL, starts_at_utc DATETIME(6) NOT NULL, interval_minutes INT NULL,
  timezone_id VARCHAR(100) NOT NULL, active_from TIME NULL, active_to TIME NULL, arrival_audio_path VARCHAR(1024) NULL,
  created_at_utc DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)), updated_at_utc DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6))
);
CREATE TABLE IF NOT EXISTS reminder_occurrences (
  id CHAR(36) PRIMARY KEY, reminder_id CHAR(36) NOT NULL, due_at_utc DATETIME(6) NOT NULL, original_due_at_utc DATETIME(6) NOT NULL,
  outcome VARCHAR(20) NOT NULL, presented_at_utc DATETIME(6) NULL, responded_at_utc DATETIME(6) NULL, snooze_minutes INT NULL,
  CONSTRAINT fk_occurrence_reminder FOREIGN KEY (reminder_id) REFERENCES reminders(id),
  INDEX ix_occurrences_pending (outcome, due_at_utc)
);
INSERT IGNORE INTO schema_versions (version, applied_at_utc) VALUES (1, UTC_TIMESTAMP(6));
