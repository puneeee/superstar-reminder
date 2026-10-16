using System.Text.Json;
using Dapper;
using MySqlConnector;
using SuperstarReminder.Domain;
namespace SuperstarReminder.Persistence;

public sealed class MySqlReminderRepository(string connectionString) : IReminderRepository
{
    private MySqlConnection Open() => new(new MySqlConnectionStringBuilder(connectionString){GuidFormat=MySqlGuidFormat.None}.ConnectionString);
    private const string SelectReminder = """
        SELECT id Id,title Title,message Message,enabled Enabled,schedule_type ScheduleType,starts_at_utc StartsAt,
        interval_minutes IntervalMinutes,timezone_id TimeZoneId,active_from ActiveFrom,active_to ActiveTo,
        arrival_audio_path ArrivalAudioPath,weekdays_json WeekdaysJson,next_due_at_utc NextDue,deleted Deleted FROM reminders
        """;
    private const string SelectOccurrence = """
        SELECT id Id,reminder_id ReminderId,due_at_utc DueAt,original_due_at_utc OriginalDueAt,outcome Outcome,
        presented_at_utc PresentedAt,responded_at_utc RespondedAt,snooze_minutes SnoozeMinutes,title_snapshot TitleSnapshot
        FROM reminder_occurrences
        """;
    public async Task<IReadOnlyList<Reminder>> GetAllAsync(CancellationToken ct)
    {
        await using var db = Open();
        return (await db.QueryAsync<ReminderRow>(new CommandDefinition(SelectReminder + " WHERE deleted=0 ORDER BY title;", cancellationToken:ct))).Select(Map).ToList();
    }
    public async Task UpsertAsync(Reminder r, CancellationToken ct)
    {
        ScheduleCalculator.Validate(r);
        await using var db = Open(); await db.OpenAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition("""
            INSERT INTO reminders(id,title,message,enabled,schedule_type,starts_at_utc,interval_minutes,timezone_id,active_from,active_to,arrival_audio_path,weekdays_json,next_due_at_utc)
            VALUES(@Id,@Title,@Message,@Enabled,@Type,@Start,@IntervalMinutes,@TimeZoneId,@ActiveFrom,@ActiveTo,@ArrivalAudioPath,@Days,@Next)
            ON DUPLICATE KEY UPDATE title=@Title,message=@Message,enabled=@Enabled,schedule_type=@Type,starts_at_utc=@Start,
            interval_minutes=@IntervalMinutes,timezone_id=@TimeZoneId,active_from=@ActiveFrom,active_to=@ActiveTo,
            arrival_audio_path=@ArrivalAudioPath,weekdays_json=@Days,next_due_at_utc=@Next,updated_at_utc=UTC_TIMESTAMP(6);
            """, new {Id=r.Id.ToString(),r.Title,r.Message,r.Enabled,Type=r.ScheduleType.ToString(),Start=r.StartsAtUtc.UtcDateTime,r.IntervalMinutes,r.TimeZoneId,
                ActiveFrom=r.ActiveFrom?.ToTimeSpan(),ActiveTo=r.ActiveTo?.ToTimeSpan(),r.ArrivalAudioPath,Days=JsonSerializer.Serialize(r.Weekdays),Next=r.NextDueAtUtc?.UtcDateTime},tx,cancellationToken:ct));
        await db.ExecuteAsync(new CommandDefinition("DELETE FROM reminder_occurrences WHERE reminder_id=@id AND outcome='Pending';",new{id=r.Id.ToString()},tx,cancellationToken:ct));
        await tx.CommitAsync(ct);
    }
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await using var db = Open(); await db.OpenAsync(ct); await using var tx = await db.BeginTransactionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition("UPDATE reminders SET deleted=1,enabled=0,next_due_at_utc=NULL WHERE id=@id; DELETE FROM reminder_occurrences WHERE reminder_id=@id AND outcome='Pending';",new{id=id.ToString()},tx,cancellationToken:ct));
        await tx.CommitAsync(ct);
    }
    public async Task GenerateDueAsync(DateTimeOffset now, CancellationToken ct)
    {
        await using var db = Open(); await db.OpenAsync(ct); await using var tx = await db.BeginTransactionAsync(ct);
        var rows = await db.QueryAsync<ReminderRow>(new CommandDefinition(SelectReminder+" WHERE enabled=1 AND deleted=0 AND next_due_at_utc <= @now FOR UPDATE;",new{now=now.UtcDateTime},tx,cancellationToken:ct));
        foreach (var row in rows)
        {
            var r=Map(row); var due=r.NextDueAtUtc!.Value;
            await Insert(db,tx,new ReminderOccurrence(Guid.NewGuid(),r.Id,due,due,ReminderOutcome.Pending){TitleSnapshot=r.Title},ct);
            var next=ScheduleCalculator.Next(r,now);
            await db.ExecuteAsync(new CommandDefinition("UPDATE reminders SET next_due_at_utc=@next WHERE id=@id;",new{next=next?.UtcDateTime,id=r.Id.ToString()},tx,cancellationToken:ct));
        }
        await tx.CommitAsync(ct);
    }
    public async Task<IReadOnlyList<ReminderOccurrence>> GetPendingDueAsync(DateTimeOffset now,CancellationToken ct)
    {
        await using var db=Open();
        return (await db.QueryAsync<OccurrenceRow>(new CommandDefinition(SelectOccurrence+" WHERE outcome='Pending' AND due_at_utc <= @now ORDER BY due_at_utc;",new{now=now.UtcDateTime},cancellationToken:ct))).Select(Map).ToList();
    }
    public async Task<IReadOnlyList<ReminderOccurrence>> GetHistoryAsync(CancellationToken ct)
    {
        await using var db=Open();
        return (await db.QueryAsync<OccurrenceRow>(new CommandDefinition(SelectOccurrence+" WHERE outcome <> 'Pending' ORDER BY responded_at_utc DESC LIMIT 500;",cancellationToken:ct))).Select(Map).ToList();
    }
    public async Task RespondAsync(ReminderOccurrence o,ReminderOutcome outcome,DateTimeOffset now,int? snoozeMinutes,CancellationToken ct)
    {
        if(outcome is not (ReminderOutcome.Yes or ReminderOutcome.No or ReminderOutcome.Unanswered or ReminderOutcome.Snoozed)) throw new ArgumentException("Invalid response.");
        if(outcome==ReminderOutcome.Snoozed && snoozeMinutes is not (5 or 10 or 20 or 30)) throw new ArgumentException("Select a snooze duration.");
        await using var db=Open(); await db.OpenAsync(ct); await using var tx=await db.BeginTransactionAsync(ct);
        var updated=await db.ExecuteAsync(new CommandDefinition("""
            UPDATE reminder_occurrences SET outcome=@outcome,presented_at_utc=@presented,responded_at_utc=@now,snooze_minutes=@snooze
            WHERE id=@id AND outcome='Pending';
            """,new{outcome=outcome.ToString(),presented=o.PresentedAtUtc?.UtcDateTime,now=now.UtcDateTime,snooze=snoozeMinutes,id=o.Id.ToString()},tx,cancellationToken:ct));
        if(updated==1 && outcome==ReminderOutcome.Snoozed)
        {
            var next=o with {Id=Guid.NewGuid(),DueAtUtc=now.AddMinutes(snoozeMinutes!.Value),Outcome=ReminderOutcome.Pending,PresentedAtUtc=null,RespondedAtUtc=null,SnoozeMinutes=snoozeMinutes};
            await Insert(db,tx,next,ct);
        }
        await tx.CommitAsync(ct);
    }
    public async Task DeferAsync(IReadOnlyList<ReminderOccurrence> occurrences,CancellationToken ct)
    {
        await using var db=Open(); await db.OpenAsync(ct); await using var tx=await db.BeginTransactionAsync(ct);
        foreach(var o in occurrences)
            await db.ExecuteAsync(new CommandDefinition("UPDATE reminder_occurrences SET outcome='Deferred',responded_at_utc=UTC_TIMESTAMP(6) WHERE id=@id AND outcome='Pending';",new{id=o.Id.ToString()},tx,cancellationToken:ct));
        await tx.CommitAsync(ct);
    }
    private static Task<int> Insert(MySqlConnection db,MySqlTransaction tx,ReminderOccurrence o,CancellationToken ct)=>db.ExecuteAsync(new CommandDefinition("""
        INSERT INTO reminder_occurrences(id,reminder_id,due_at_utc,original_due_at_utc,outcome,snooze_minutes,title_snapshot)
        VALUES(@id,@rid,@due,@original,'Pending',@snooze,@title)
        ON DUPLICATE KEY UPDATE id=id;
        """,new{id=o.Id.ToString(),rid=o.ReminderId.ToString(),due=o.DueAtUtc.UtcDateTime,original=o.OriginalDueAtUtc.UtcDateTime,snooze=o.SnoozeMinutes,title=o.TitleSnapshot},tx,cancellationToken:ct));
    private static DateTimeOffset Utc(DateTime d)=>new(DateTime.SpecifyKind(d,DateTimeKind.Utc));
    private static Reminder Map(ReminderRow x)=>new(Guid.Parse(x.Id),x.Title,x.Message,x.Enabled,Enum.Parse<ScheduleType>(x.ScheduleType),Utc(x.StartsAt),x.IntervalMinutes,
        (JsonSerializer.Deserialize<DayOfWeek[]>(x.WeekdaysJson)??[]).ToHashSet(),x.ActiveFrom.HasValue?TimeOnly.FromTimeSpan(x.ActiveFrom.Value):null,x.ActiveTo.HasValue?TimeOnly.FromTimeSpan(x.ActiveTo.Value):null,x.TimeZoneId,x.ArrivalAudioPath){NextDueAtUtc=x.NextDue.HasValue?Utc(x.NextDue.Value):null,Deleted=x.Deleted};
    private static ReminderOccurrence Map(OccurrenceRow x)=>new(Guid.Parse(x.Id),Guid.Parse(x.ReminderId),Utc(x.DueAt),Utc(x.OriginalDueAt),Enum.Parse<ReminderOutcome>(x.Outcome),x.PresentedAt.HasValue?Utc(x.PresentedAt.Value):null,x.RespondedAt.HasValue?Utc(x.RespondedAt.Value):null,x.SnoozeMinutes){TitleSnapshot=x.TitleSnapshot};
    private sealed class ReminderRow
    {
        public string Id{get;set;}=""; public string Title{get;set;}=""; public string Message{get;set;}=""; public bool Enabled{get;set;} public string ScheduleType{get;set;}=""; public DateTime StartsAt{get;set;} public int? IntervalMinutes{get;set;} public string TimeZoneId{get;set;}=""; public TimeSpan? ActiveFrom{get;set;} public TimeSpan? ActiveTo{get;set;} public string? ArrivalAudioPath{get;set;} public string WeekdaysJson{get;set;}="[]"; public DateTime? NextDue{get;set;} public bool Deleted{get;set;}
    }
    private sealed class OccurrenceRow
    {
        public string Id{get;set;}="";public string ReminderId{get;set;}="";public DateTime DueAt{get;set;}public DateTime OriginalDueAt{get;set;}public string Outcome{get;set;}="";public DateTime? PresentedAt{get;set;}public DateTime? RespondedAt{get;set;}public int? SnoozeMinutes{get;set;}public string TitleSnapshot{get;set;}="";
    }
}
