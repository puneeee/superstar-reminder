using SuperstarReminder.Domain;

namespace SuperstarReminder.Services;

public interface IClock { DateTimeOffset UtcNow { get; } }
public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }

public interface IReminderCoordinator
{
    Task<IReadOnlyList<ReminderOccurrence>> FindDueAsync(CancellationToken cancellationToken);
    Task<ReminderOccurrence> RespondAsync(ReminderOccurrence occurrence, ReminderOutcome outcome, int? snoozeMinutes, CancellationToken cancellationToken);
}

public interface ICharacterCatalog
{
    IReadOnlyCollection<CharacterDefinition> All { get; }
    CharacterDefinition Default { get; }
}

public interface IOverlayHost
{
    Task<ReminderOutcome> PresentAsync(OverlayRequest request, CancellationToken cancellationToken);
}

public interface INotificationService
{
    Task ShowCatchUpAsync(IReadOnlyList<ReminderOccurrence> occurrences, CancellationToken cancellationToken);
}

public interface IAudioPlayer
{
    Task PlayArrivalAsync(string? path, CancellationToken cancellationToken);
    Task StopAsync();
}

public interface ISessionMonitor
{
    bool CanPresentOverlay { get; }
    event EventHandler<bool>? PresentationAvailabilityChanged;
}

public interface IAutoStartService { Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken); }
public interface ISecretStore { Task SaveAsync(string name, string value, CancellationToken cancellationToken); Task<string?> GetAsync(string name, CancellationToken cancellationToken); }
