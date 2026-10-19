namespace SuperstarReminder.Domain;

public enum OverlayStage { Hidden, Arriving, Landing, Flourish, AwaitingResponse, Reacting, Saluting, Departing }

/// <summary>All coordinates and speeds are logical pixels; the desktop adapter applies monitor scaling.</summary>
public static class OverlayPresentation
{
    public const double DefaultHeightFraction = .30;
    public const double HeadInsetFraction = .035;
    public const double BubbleGap = 6;
    public const double HappyWalkSpeed = 185;
    public const double SadWalkSpeed = 85;
    public static double CharacterHeight(double workingHeight, double fraction) =>
        Math.Clamp(workingHeight * Math.Clamp(fraction, .18, .40), Math.Min(180, workingHeight * .40), Math.Min(440, workingHeight * .40));
    public static double BannerTop(double characterTop, double characterHeight, double bannerHeight) =>
        characterTop + characterHeight * HeadInsetFraction - BubbleGap - bannerHeight;
    public static int WalkMilliseconds(double distance, bool happy) =>
        (int)Math.Clamp(Math.Abs(distance) / (happy ? HappyWalkSpeed : SadWalkSpeed) * 1000, 900, 14000);
    public static string ExitAction(ReminderOutcome outcome, bool left) =>
        outcome == ReminderOutcome.Yes ? (left ? "walkLeft" : "walkRight") : (left ? "sadWalkLeft" : "sadWalkRight");
}
