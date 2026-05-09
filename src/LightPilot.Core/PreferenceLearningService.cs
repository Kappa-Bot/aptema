namespace LightPilot.Core;

public static class PreferenceLearningService
{
    private const int MaxAggregates = 200;
    private const int BrightnessOffsetPerScore = 2;
    private const int WarmthOffsetPerScoreKelvin = 80;

    public static PreferenceLearningModel RecordFeedback(
        PreferenceLearningModel? model,
        PreferenceLearningContext context,
        ComfortFeedback feedback,
        DateTimeOffset now)
    {
        model ??= PreferenceLearningModel.Empty;
        var aggregates = model.Aggregates.ToDictionary(aggregate => aggregate.Key, StringComparer.OrdinalIgnoreCase);
        aggregates.TryGetValue(context.Key, out var current);

        var (brightnessScore, warmthScore, perfect) = feedback switch
        {
            ComfortFeedback.TooBright => (-1, -1, 0),
            ComfortFeedback.TooDim => (1, 1, 0),
            ComfortFeedback.TooWarm => (0, 1, 0),
            ComfortFeedback.TooCold => (0, -1, 0),
            ComfortFeedback.Perfect => (0, 0, 1),
            _ => (0, 0, 0)
        };

        var updated = Recalculate(new PreferenceCorrectionAggregate
        {
            Key = context.Key,
            MonitorId = context.MonitorId,
            AppCategory = context.AppCategory,
            DayPhase = context.DayPhase,
            IsFullscreen = context.IsFullscreen,
            Luminance = context.Luminance,
            Samples = (current?.Samples ?? 0) + 1,
            PerfectCount = (current?.PerfectCount ?? 0) + perfect,
            NetBrightnessScore = (current?.NetBrightnessScore ?? 0) + brightnessScore,
            NetWarmthScore = (current?.NetWarmthScore ?? 0) + warmthScore,
            LastUpdatedAt = now
        });

        aggregates[context.Key] = updated;

        var ordered = aggregates.Values
            .OrderByDescending(aggregate => aggregate.LastUpdatedAt)
            .ThenBy(aggregate => aggregate.Key, StringComparer.Ordinal)
            .Take(MaxAggregates)
            .ToArray();

        return model with { Aggregates = ordered };
    }

    public static PreferenceAdjustment GetAdjustment(PreferenceLearningModel? model, PreferenceLearningContext context)
    {
        if (model is null)
        {
            return PreferenceAdjustment.None;
        }

        var aggregate = model.Aggregates.FirstOrDefault(item => string.Equals(item.Key, context.Key, StringComparison.OrdinalIgnoreCase));
        if (aggregate is null || aggregate.Samples <= 0)
        {
            return PreferenceAdjustment.None;
        }

        return new PreferenceAdjustment(
            aggregate.BrightnessOffsetPercent,
            aggregate.WarmthOffsetKelvin,
            aggregate.Confidence,
            true);
    }

    private static PreferenceCorrectionAggregate Recalculate(PreferenceCorrectionAggregate aggregate)
    {
        var brightnessOffset = Math.Clamp(aggregate.NetBrightnessScore * BrightnessOffsetPerScore, -12, 12);
        var warmthOffset = Math.Clamp(aggregate.NetWarmthScore * WarmthOffsetPerScoreKelvin, -480, 480);
        var sampleConfidence = Math.Min(aggregate.Samples / 6d, 1d);
        var strongestAxis = Math.Max(Math.Abs(aggregate.NetBrightnessScore), Math.Abs(aggregate.NetWarmthScore));
        var directionalConfidence = aggregate.Samples == 0 ? 0 : strongestAxis / (double)aggregate.Samples;
        var perfectConfidence = aggregate.Samples == 0 ? 0 : aggregate.PerfectCount / (double)aggregate.Samples * 0.75;
        var confidence = Math.Clamp(sampleConfidence * Math.Max(directionalConfidence, perfectConfidence), 0, 1);

        return aggregate with
        {
            BrightnessOffsetPercent = brightnessOffset,
            WarmthOffsetKelvin = warmthOffset,
            Confidence = confidence
        };
    }
}
