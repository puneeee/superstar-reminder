namespace SuperstarReminder.Domain;

public enum CharacterAction { Enter, Idle, Happy, RunLeft, RunRight, WalkLeft, WalkRight, Land, Flourish, Salute, Sad, SadWalkLeft, SadWalkRight }

public sealed record CharacterDefinition(
    string Id,
    string DisplayName,
    string ManifestPath,
    IReadOnlySet<CharacterAction> SupportedActions,
    string BannerAnchor,
    double DefaultHeightFraction);

public sealed record OverlayRequest(
    ReminderOccurrence Occurrence,
    Reminder Reminder,
    CharacterDefinition Character,
    ScreenCorner Corner,
    TimeSpan ResponseTimeout);
