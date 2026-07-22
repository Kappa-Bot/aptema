namespace Aptema.Core;

public static class ComfortCopy
{
    public static string DescribeIntensity(int intensity)
    {
        return Math.Clamp(intensity, 0, 100) switch
        {
            < 35 => "Light",
            < 65 => "Balanced",
            _ => "Deep comfort"
        };
    }

    public static string DescribeLightLevel(int brightnessPercent)
    {
        return Math.Clamp(brightnessPercent, 0, 100) switch
        {
            >= 82 => "Bright",
            >= 58 => "Comfortable",
            >= 38 => "Soft",
            _ => "Very soft"
        };
    }

    public static string DescribeWarmth(int colorTemperatureKelvin)
    {
        return colorTemperatureKelvin switch
        {
            >= 5600 => "Clear",
            >= 4500 => "Soft",
            >= 3600 => "Warm",
            _ => "Deep warm"
        };
    }

    public static string DescribeReason(ComfortDecision decision)
    {
        if (decision.ReasonCodes.Contains("CooldownActive", StringComparer.OrdinalIgnoreCase))
        {
            return "Adjusting softly";
        }

        if (decision.ReasonCodes.Contains("NoChangeWithinHysteresis", StringComparer.OrdinalIgnoreCase))
        {
            return "Comfort steady";
        }

        return decision.Reason switch
        {
            "Bright reading content at night" => "Bright reading at night",
            "Late development session" => "Late work session",
            "Gaming fullscreen protection" => "Game protected",
            "Video playback protected" => "Video protected",
            "Auto is paused" => "Paused",
            "Manual adjustment cooldown" => "Manual choice protected",
            _ => decision.Reason
        };
    }
}
