using System.Collections.ObjectModel;
using Aptema.Application;
using Aptema.Core;

namespace Aptema.App.ViewModels;

public sealed class DisplaySettingsViewModel
{
    public DisplaySettingsViewModel(UserSettings settings, IReadOnlyList<MonitorModel> monitors)
    {
        Displays = new ObservableCollection<DisplayConfigurationViewModel>(
            monitors.Select((monitor, index) => new DisplayConfigurationViewModel(index + 1, monitor, Find(settings, monitor), null)));
    }

    public DisplaySettingsViewModel(UserSettings settings, IReadOnlyList<DisplayRuntimeState> displays)
    {
        Displays = new ObservableCollection<DisplayConfigurationViewModel>(
            displays.Select((display, index) => new DisplayConfigurationViewModel(index + 1, display.Monitor, Find(settings, display.Monitor), display.ApplyResult)));
    }

    public ObservableCollection<DisplayConfigurationViewModel> Displays { get; }

    public UserSettings ToSettings(UserSettings current) => current with
    {
        DisplayConfigurations = Displays.Select(display => display.ToConfiguration()).ToArray()
    };

    private static DisplayConfiguration? Find(UserSettings settings, MonitorModel monitor)
    {
        var keys = monitor.Aliases.Append(monitor.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return settings.DisplayConfigurations.FirstOrDefault(item => keys.Contains(item.StableId) || item.LegacyAliases.Any(keys.Contains));
    }
}

public sealed class DisplayConfigurationViewModel : ObservableObject
{
    private bool _isEnabled;
    private int _brightnessOffsetPercent;
    private int _minimumBrightnessPercent;
    private int _maximumBrightnessPercent;

    public DisplayConfigurationViewModel(int number, MonitorModel monitor, DisplayConfiguration? configuration, BrightnessApplyResult? result)
    {
        Number = number;
        Monitor = monitor;
        _isEnabled = configuration?.IsEnabled ?? true;
        _brightnessOffsetPercent = configuration?.BrightnessOffsetPercent ?? monitor.BrightnessOffsetPercent;
        _minimumBrightnessPercent = configuration?.MinimumBrightnessPercent ?? monitor.MinimumBrightnessPercent;
        _maximumBrightnessPercent = configuration?.MaximumBrightnessPercent ?? monitor.MaximumBrightnessPercent;
        AllowSoftwareFallback = configuration?.AllowSoftwareFallback ?? true;
        ControlMethod = DescribeMethod(result, monitor);
        Status = DescribeStatus(result);
        Guidance = DescribeGuidance(result, monitor);
    }

    public int Number { get; }
    public MonitorModel Monitor { get; }
    public string Name => Monitor.DisplayName;
    public string NumberLabel => $"Display {Number}";
    public string ControlMethod { get; }
    public string Status { get; }
    public string Guidance { get; }
    public bool AllowSoftwareFallback { get; set; }

    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }
    public int BrightnessOffsetPercent { get => _brightnessOffsetPercent; set => SetProperty(ref _brightnessOffsetPercent, Math.Clamp(value, -20, 20)); }
    public int MinimumBrightnessPercent
    {
        get => _minimumBrightnessPercent;
        set
        {
            if (SetProperty(ref _minimumBrightnessPercent, Math.Clamp(value, 15, 100)) && _maximumBrightnessPercent < _minimumBrightnessPercent)
                MaximumBrightnessPercent = _minimumBrightnessPercent;
        }
    }
    public int MaximumBrightnessPercent
    {
        get => _maximumBrightnessPercent;
        set
        {
            if (SetProperty(ref _maximumBrightnessPercent, Math.Clamp(value, 15, 100)) && _minimumBrightnessPercent > _maximumBrightnessPercent)
                MinimumBrightnessPercent = _maximumBrightnessPercent;
        }
    }

    public DisplayConfiguration ToConfiguration() => new(
        Monitor.Id, Monitor.Aliases.ToArray(), IsEnabled, BrightnessOffsetPercent,
        MinimumBrightnessPercent, MaximumBrightnessPercent, AllowSoftwareFallback);

    private static string DescribeMethod(BrightnessApplyResult? result, MonitorModel monitor) => result?.AppliedLayer switch
    {
        BrightnessControlLayer.DdcCi => "Direct monitor control",
        BrightnessControlLayer.WindowsBrightness => "Windows brightness",
        BrightnessControlLayer.Overlay => "Gentle screen filter",
        _ when monitor.SupportsBrightnessControl => "Direct monitor control available",
        _ => "Gentle screen filter"
    };

    private static string DescribeStatus(BrightnessApplyResult? result) => result?.State switch
    {
        MonitorControlState.Failed => "Needs attention",
        MonitorControlState.Degraded or MonitorControlState.FallbackUsed => "Using fallback",
        MonitorControlState.Disabled => "Off",
        _ => "Ready"
    };

    private static string DescribeGuidance(BrightnessApplyResult? result, MonitorModel monitor) => result?.State switch
    {
        MonitorControlState.Failed => "Reconnect the display or enable the gentle screen filter.",
        MonitorControlState.Degraded or MonitorControlState.FallbackUsed => "Aptema is protecting this display with a software fallback.",
        _ when !monitor.SupportsBrightnessControl => "Direct brightness is unavailable; the local screen filter remains safe.",
        _ => "Aptema can adjust this display directly."
    };
}
