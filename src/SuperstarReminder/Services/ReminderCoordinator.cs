using SuperstarReminder.Domain;
using SuperstarReminder.Persistence;
namespace SuperstarReminder.Services;
public sealed class ReminderCoordinator(IReminderRepository repository,IClock clock):IReminderCoordinator
{
    public async Task<IReadOnlyList<ReminderOccurrence>> FindDueAsync(CancellationToken ct)
    {
        await repository.GenerateDueAsync(clock.UtcNow,ct);
        return await repository.GetPendingDueAsync(clock.UtcNow,ct);
    }
    public async Task<ReminderOccurrence> RespondAsync(ReminderOccurrence occurrence,ReminderOutcome outcome,int? snoozeMinutes,CancellationToken ct)
    {
        await repository.RespondAsync(occurrence,outcome,clock.UtcNow,snoozeMinutes,ct);
        return occurrence with{Outcome=outcome,RespondedAtUtc=clock.UtcNow,SnoozeMinutes=snoozeMinutes};
    }
}
