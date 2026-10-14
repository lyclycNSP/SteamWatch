using SteamWatch.Core.Settings;

namespace SteamWatch.App.Presentation;

public sealed record GeneralSettingsEdit(CloseWindowAction CloseAction, bool StartWithWindows, bool PlaySounds)
{
    public AppSettings Merge(AppSettings current) => current with
    {
        CloseWindowAction = CloseAction, StartWithWindows = StartWithWindows, PlayReminderSounds = PlaySounds
    };
}

public sealed record ReminderSettingsEdit(double Countdown, double First, double Second, double Final)
{
    public static ReminderSettingsEdit From(AppSettings settings) => new(settings.ForceCloseCountdownSeconds,
        settings.FirstReminderThresholdPercent, settings.SecondReminderThresholdPercent, settings.FinalReminderThresholdPercent);

    public AppSettings Merge(AppSettings current)
    {
        if (!IsInteger(Countdown) || Countdown < 5 || Countdown > 600)
            throw new ArgumentException("强退倒计时必须为 5–600 之间的整数秒。");
        if (!IsInteger(First) || !IsInteger(Second) || !IsInteger(Final)
            || First <= 0 || First >= Second || Second >= Final || Final >= 100)
            throw new ArgumentException("提醒阈值必须为整数，且满足：0 < 一级 < 二级 < 最终 < 100。");
        return current with
        {
            ForceCloseCountdownSeconds = (int)Countdown, FirstReminderThresholdPercent = (int)First,
            SecondReminderThresholdPercent = (int)Second, FinalReminderThresholdPercent = (int)Final
        };
    }

    private static bool IsInteger(double value) => double.IsFinite(value) && value == Math.Truncate(value);
}
