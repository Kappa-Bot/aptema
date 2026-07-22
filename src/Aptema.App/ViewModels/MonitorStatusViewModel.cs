namespace Aptema.App.ViewModels;

public sealed class MonitorStatusViewModel : ObservableObject
{
    private int _brightnessPercent;
    private int _colorTemperatureKelvin;
    private string _controlLayer = "";
    private string _status = "";
    private string _lightLevel = "";

    public MonitorStatusViewModel(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public int BrightnessPercent
    {
        get => _brightnessPercent;
        set => SetProperty(ref _brightnessPercent, value);
    }

    public int ColorTemperatureKelvin
    {
        get => _colorTemperatureKelvin;
        set => SetProperty(ref _colorTemperatureKelvin, value);
    }

    public string ControlLayer
    {
        get => _controlLayer;
        set => SetProperty(ref _controlLayer, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string LightLevel
    {
        get => _lightLevel;
        set => SetProperty(ref _lightLevel, value);
    }
}
