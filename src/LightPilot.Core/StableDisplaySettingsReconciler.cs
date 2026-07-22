namespace LightPilot.Core;

public static class StableDisplaySettingsReconciler
{
    public static UserSettings Reconcile(UserSettings settings, IReadOnlyList<MonitorModel> monitors)
    {
        var aliasesToStable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var monitor in monitors)
        {
            aliasesToStable[monitor.Id] = monitor.Id;
            foreach (var alias in monitor.Aliases) aliasesToStable[alias] = monitor.Id;
        }

        var changed = false;
        var displays = settings.DisplayConfigurations.Select(configuration =>
        {
            var candidateKeys = configuration.LegacyAliases.Append(configuration.StableId);
            var stableId = candidateKeys.Select(key => aliasesToStable.GetValueOrDefault(key)).FirstOrDefault(value => value is not null);
            if (stableId is null || string.Equals(stableId, configuration.StableId, StringComparison.OrdinalIgnoreCase)) return configuration;
            changed = true;
            return configuration with
            {
                StableId = stableId,
                LegacyAliases = configuration.LegacyAliases.Append(configuration.StableId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            };
        }).ToArray();

        var learning = settings.PreferenceLearning.Aggregates.Select(aggregate =>
        {
            if (!aliasesToStable.TryGetValue(aggregate.MonitorId, out var stableId) ||
                string.Equals(stableId, aggregate.MonitorId, StringComparison.OrdinalIgnoreCase)) return aggregate;
            changed = true;
            var context = new PreferenceLearningContext(stableId, aggregate.AppCategory, aggregate.DayPhase, aggregate.IsFullscreen, aggregate.Luminance);
            return aggregate with { MonitorId = stableId, Key = context.Key };
        }).ToArray();

        return changed
            ? settings with { DisplayConfigurations = displays, PreferenceLearning = settings.PreferenceLearning with { Aggregates = learning } }
            : settings;
    }
}
