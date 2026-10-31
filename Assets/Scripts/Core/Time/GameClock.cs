using System;

/// <summary>Zero-based elapsed minutes, fixed 365-day years. No system calendar dependency.</summary>
public sealed class GameClock
{
    public const long MinutesPerDay = 1440, MinutesPerYear = 525600;
    private static readonly int[] MonthDays = {31,28,31,30,31,30,31,31,30,31,30,31};
    public long CurrentGameMinute { get; private set; }
    public long Year => CurrentGameMinute / MinutesPerYear + 1;
    public int DayOfYear => (int)(CurrentGameMinute / MinutesPerDay % 365) + 1;
    public int Hour => (int)(CurrentGameMinute / 60 % 24);
    public int Minute => (int)(CurrentGameMinute % 60);
    public int Month { get { int d = DayOfYear, m = 0; while (d > MonthDays[m]) d -= MonthDays[m++]; return m + 1; } }
    public int Day { get { int d = DayOfYear, m = 0; while (d > MonthDays[m]) d -= MonthDays[m++]; return d; } }
    internal static GameClock Restore(long minute)
    {
        if (minute < 0) throw new ArgumentOutOfRangeException(nameof(minute));
        return new GameClock { CurrentGameMinute = minute };
    }
    internal void Commit(long minute)
    {
        if (minute != checked(CurrentGameMinute + 1)) throw new InvalidOperationException("Only sequential minute commits are allowed.");
        CurrentGameMinute = minute;
    }
    public static long ToMinute(long year, int month, int day, int hour, int minute)
    {
        if (month < 1 || month > 12)
            throw new ArgumentOutOfRangeException(nameof(month), "월은 1~12 사이여야 합니다.");
        if (day < 1 || day > MonthDays[month - 1])
            throw new ArgumentOutOfRangeException(nameof(day), $"{month}월은 1~{MonthDays[month - 1]}일까지 입력할 수 있습니다.");
        int dayOfYear = day;
        for (int i = 0; i < month - 1; i++) dayOfYear += MonthDays[i];
        return ToMinute(year, dayOfYear, hour, minute);
    }
    public static long ToMinute(long year, int dayOfYear, int hour, int minute)
    {
        if (year < 1 || dayOfYear < 1 || dayOfYear > 365 || hour < 0 || hour > 23 || minute < 0 || minute > 59)
            throw new ArgumentOutOfRangeException("Date must be year >= 1, day 1..365, hour 0..23, minute 0..59.");
        return checked((year - 1) * MinutesPerYear + (dayOfYear - 1) * MinutesPerDay + hour * 60 + minute);
    }
    public override string ToString() => $"{Year}년차 {Month:00}월 {Day:00}일 {Hour:00}:{Minute:00}";
}
