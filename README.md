# Light Pilot

![Light Pilot logo](src/LightPilot.App/Assets/LightPilotLogo.png)

**Light Pilot** is a local-first Windows screen comfort utility. It runs quietly in the tray, adapts brightness and warmth gradually, protects games/videos from disruptive changes, and keeps all screen analysis on-device.

Think CareUEyes/f.lux-style comfort, with a calmer adaptive engine and a cleaner professional UI.

![Light Pilot main window](docs/assets/light-pilot-main.png)

## Highlights

- Tray-first Windows desktop app
- Compact GUI with quick comfort presets: Light, Balanced, Deep
- Gentle adaptive brightness: max 3 percentage points per decision
- Gentle warmth transitions: max 200K per decision
- Less aggressive default comfort intensity
- DDC/CI monitor brightness when supported
- WMI laptop brightness fallback
- Smooth warm overlay for perceived color temperature
- First-run onboarding
- Too dim / Too bright preference feedback
- Per-display rules: offset or disable control for a display
- Battery-aware lighter checks
- Inno Setup installer script and GitHub release automation
- Optional local-only content brightness analysis, off by default
- Startup registration with background launch
- Single-instance behavior: opening the app brings the existing tray instance forward
- MIT licensed

## Privacy

Light Pilot is local-first.

- No cloud usage
- No telemetry
- No screenshot storage
- No clipboard usage
- Optional content brightness analysis only computes in-memory luminance aggregates

## Run From Source

```powershell
dotnet build LightPilot.sln
dotnet run --project src/LightPilot.App/LightPilot.App.csproj
```

Background/tray mode:

```powershell
dotnet run --project src/LightPilot.App/LightPilot.App.csproj -- --background
```

Safe no-hardware mode:

```powershell
dotnet run --project src/LightPilot.App/LightPilot.App.csproj -- --no-hardware
```

## Test

```powershell
dotnet test LightPilot.sln
```

## Package

```powershell
.\scripts\package-release.ps1 -Version 0.2.1
```

Build installer too when Inno Setup 6 is installed:

```powershell
.\scripts\package-release.ps1 -Version 0.2.1 -BuildInstaller
```

## Local Install

```powershell
.\scripts\install-local.ps1
```

This installs to `%LOCALAPPDATA%\LightPilot\App`, creates a Start Menu shortcut, starts the tray app, and registers startup with `--background`.

Uninstall local app files:

```powershell
.\scripts\uninstall-local.ps1
```

Smoke check:

```powershell
.\scripts\smoke.ps1
```

Update local install from the latest GitHub release:

```powershell
.\scripts\update-local.ps1
```

## License

MIT License. Copyright (c) 2026 edfpolo.
