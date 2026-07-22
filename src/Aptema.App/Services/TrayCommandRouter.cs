namespace Aptema.App.Services;

public interface ITrayCommandTarget
{
    void ToggleAuto();
    void PauseOrResume();
    void PauseUntilTomorrow();
    void TooBright();
    void TooDim();
    void Warmer();
    void Cooler();
    void Perfect();
    void Open();
    void Settings();
    void ShortcutHelp();
    void Exit();
}

public sealed class TrayCommandRouter(ITrayCommandTarget target)
{
    public void Execute(TrayMenuCommandKey command)
    {
        switch (command)
        {
            case TrayMenuCommandKey.ToggleAuto: target.ToggleAuto(); break;
            case TrayMenuCommandKey.PauseOrResume: target.PauseOrResume(); break;
            case TrayMenuCommandKey.PauseUntilTomorrow: target.PauseUntilTomorrow(); break;
            case TrayMenuCommandKey.TooBright: target.TooBright(); break;
            case TrayMenuCommandKey.TooDim: target.TooDim(); break;
            case TrayMenuCommandKey.Warmer: target.Warmer(); break;
            case TrayMenuCommandKey.Cooler: target.Cooler(); break;
            case TrayMenuCommandKey.Perfect: target.Perfect(); break;
            case TrayMenuCommandKey.Open: target.Open(); break;
            case TrayMenuCommandKey.Settings: target.Settings(); break;
            case TrayMenuCommandKey.ShortcutHelp: target.ShortcutHelp(); break;
            case TrayMenuCommandKey.Exit: target.Exit(); break;
        }
    }
}
