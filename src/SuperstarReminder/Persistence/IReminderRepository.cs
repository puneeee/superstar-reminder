using SuperstarReminder.Domain;
namespace SuperstarReminder.Persistence;
public interface IReminderRepository
{
    Task<IReadOnlyList<Reminder>> GetAllAsync(CancellationToken ct);
    Task UpsertAsync(Reminder reminder, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task GenerateDueAsync(DateTimeOffset now, CancellationToken ct);
    Task<IReadOnlyList<ReminderOccurrence>> GetPendingDueAsync(DateTimeOffset now, CancellationToken ct);
    Task<IReadOnlyList<ReminderOccurrence>> GetHistoryAsync(CancellationToken ct);
    Task RespondAsync(ReminderOccurrence occurrence, ReminderOutcome outcome, DateTimeOffset now, int? snoozeMinutes, CancellationToken ct);
    Task DeferAsync(IReadOnlyList<ReminderOccurrence> occurrences, CancellationToken ct);
}
