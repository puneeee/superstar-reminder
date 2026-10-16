namespace SuperstarReminder.Domain;

public enum ScheduleType { OneTime, Daily, Weekdays, Interval }
public enum ReminderOutcome { Pending, Yes, No, Snoozed, Unanswered, Deferred }
public enum ScreenCorner { BottomRight, BottomLeft }

public sealed record Reminder(
    Guid Id,
    string Title,
    string Message,
    bool Enabled,
    ScheduleType ScheduleType,
    DateTimeOffset StartsAtUtc,
    int? IntervalMinutes,
    IReadOnlySet<DayOfWeek> Weekdays,
    TimeOnly? ActiveFrom,
    TimeOnly? ActiveTo,
    string TimeZoneId,
    string? ArrivalAudioPath)
{
    public DateTimeOffset? NextDueAtUtc { get; init; } = StartsAtUtc;
    public bool Deleted { get; init; }
}

public sealed record ReminderOccurrence(
    Guid Id,
    Guid ReminderId,
    DateTimeOffset DueAtUtc,
    DateTimeOffset OriginalDueAtUtc,
    ReminderOutcome Outcome,
    DateTimeOffset? PresentedAtUtc = null,
    DateTimeOffset? RespondedAtUtc = null,
    int? SnoozeMinutes = null)
{
    public string TitleSnapshot { get; init; } = "Reminder";
}

public static class ReminderFactory
{
    public static Reminder WaterEvery(int minutes) => new(
        Guid.NewGuid(), "Drink water", "Time to drink water. Ready?", true,
        ScheduleType.Interval, DateTimeOffset.UtcNow.AddMinutes(minutes), minutes,
        new HashSet<DayOfWeek>(), null, null, TimeZoneInfo.Local.Id, null);
}
