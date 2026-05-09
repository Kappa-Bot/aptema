namespace LightPilot.Core;

public static class DayPhasePolicy
{
    public static DayPhase GetPhase(DateTimeOffset now)
    {
        return GetPhase(now.TimeOfDay);
    }

    public static DayPhase GetPhase(TimeSpan time)
    {
        if (time >= TimeSpan.FromHours(8) && time < TimeSpan.FromHours(17.5))
        {
            return DayPhase.Day;
        }

        if (time >= TimeSpan.FromHours(17.5) && time < TimeSpan.FromHours(21.5))
        {
            return DayPhase.Evening;
        }

        return DayPhase.Night;
    }
}
