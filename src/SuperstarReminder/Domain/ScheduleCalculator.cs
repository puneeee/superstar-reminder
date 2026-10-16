namespace SuperstarReminder.Domain;

public static class ScheduleCalculator
{
    public static void Validate(Reminder r)
    {
        if (string.IsNullOrWhiteSpace(r.Title) || r.Title.Length > 120) throw new ArgumentException("Enter a title of 1–120 characters.");
        if (string.IsNullOrWhiteSpace(r.Message) || r.Message.Length > 500) throw new ArgumentException("Enter a message of 1–500 characters.");
        if (r.ScheduleType == ScheduleType.Interval && (r.IntervalMinutes is null or < 1 or > 525600)) throw new ArgumentException("Interval must be between 1 and 525600 minutes.");
        if (r.ScheduleType == ScheduleType.Weekdays && r.Weekdays.Count == 0) throw new ArgumentException("Select at least one weekday.");
        if (r.ActiveFrom.HasValue != r.ActiveTo.HasValue || r.ActiveFrom == r.ActiveTo && r.ActiveFrom.HasValue) throw new ArgumentException("Choose different start and end times for active hours.");
        _ = TimeZoneInfo.FindSystemTimeZoneById(r.TimeZoneId);
        if (r.ScheduleType == ScheduleType.OneTime && !IsActive(r, r.StartsAtUtc)) throw new ArgumentException("The one-time reminder must fall within its active hours.");
    }

    public static bool IsActive(Reminder r, DateTimeOffset instant)
    {
        if (r.ActiveFrom is not { } from || r.ActiveTo is not { } to) return true;
        var time = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone(r)).DateTime);
        return from < to ? time >= from && time < to : time >= from || time < to;
    }

    // Returns the first occurrence strictly after 'after'. Cadence stays anchored to StartsAtUtc.
    public static DateTimeOffset? Next(Reminder r, DateTimeOffset after)
    {
        if (!r.Enabled) return null;
        if (r.ScheduleType == ScheduleType.OneTime) return r.StartsAtUtc > after && IsActive(r, r.StartsAtUtc) ? r.StartsAtUtc : null;
        if (r.ScheduleType == ScheduleType.Interval)
        {
            var interval = TimeSpan.FromMinutes(r.IntervalMinutes ?? throw new ArgumentException("Missing interval."));
            if (interval <= TimeSpan.Zero) throw new ArgumentException("Invalid interval.");
            var n = Math.Max(0L, (after.UtcTicks - r.StartsAtUtc.UtcTicks) / interval.Ticks + (after >= r.StartsAtUtc ? 1 : 0));
            var candidate = r.StartsAtUtc.AddTicks(n * interval.Ticks);
            // A one-year interval and narrow active hours may take years to intersect.
            for (var i = 0; i < 527040 && candidate <= after.AddYears(8); i++, candidate = candidate.Add(interval))
            {
                if (IsActive(r, candidate)) return candidate;
                // Skip directly to the next opening, while preserving the original interval anchor.
                var local = TimeZoneInfo.ConvertTime(candidate, Zone(r)).DateTime;
                var open = local.Date + r.ActiveFrom!.Value.ToTimeSpan();
                if (open <= local) open = open.AddDays(1);
                var openUtc = ToUtc(open, Zone(r));
                var skip = Math.Max(1, (long)Math.Ceiling((openUtc - candidate).Ticks / (double)interval.Ticks));
                candidate = candidate.AddTicks((skip - 1) * interval.Ticks);
            }
            return null;
        }
        var zone = Zone(r);
        var anchor = TimeZoneInfo.ConvertTime(r.StartsAtUtc, zone).DateTime;
        var first = TimeZoneInfo.ConvertTime(after < r.StartsAtUtc ? r.StartsAtUtc : after, zone).Date;
        for (var i = 0; i < 370; i++)
        {
            var local = first.AddDays(i) + anchor.TimeOfDay;
            if (r.ScheduleType == ScheduleType.Weekdays && !r.Weekdays.Contains(local.DayOfWeek)) continue;
            var candidate = ToUtc(local, zone);
            if (candidate >= r.StartsAtUtc && candidate > after && IsActive(r, candidate)) return candidate;
        }
        return null;
    }

    public static DateTimeOffset ToUtc(DateTime local, TimeZoneInfo zone)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        // Shift nonexistent spring-forward wall times to the first valid minute.
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(1);
        // Resolve fall-back ambiguity once, to the earlier instant.
        if (zone.IsAmbiguousTime(local)) return new DateTimeOffset(local, zone.GetAmbiguousTimeOffsets(local).Max()).ToUniversalTime();
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }
    private static TimeZoneInfo Zone(Reminder r) => TimeZoneInfo.FindSystemTimeZoneById(r.TimeZoneId);
}
