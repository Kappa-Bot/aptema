# Light Pilot

![Light Pilot logo](src/LightPilot.App/Assets/LightPilotLogo.png)

**Light Pilot** is a local-first Windows eye-comfort assistant. It runs from the tray, keeps screens comfortable automatically, and learns from quick corrections such as `Too bright`, `Too dim`, and `Perfect`.

It is designed to feel quieter and more personal than CareUEyes Lite, f.lux, or Windows Night Light: Auto on, tray running, minimal UI, gradual adjustments.

## Highlights

- Premium tray-first WPF app for Windows
- 3-surface UI: Comfort Home, Quick Adjust, Settings
- Contextual preference learning stored locally as aggregates only
- Gentle automatic brightness: max 3 percentage points per decision
- Gentle warmth: max 200K per decision
- Manual corrections are capped and applied gradually
- Fullscreen game/video/presentation protection
- DDC/CI monitor brightness when supported
- WMI laptop brightness fallback
- Software overlay fallback when hardware control is unavailable
- Optional local-only content brightness analysis, off by default
- Startup registration with `--background`
- Single-instance tray behavior
- MIT licensed

## Privacy

Light Pilot is local-first.

- No cloud usage
- No telemetry
- No screenshot storage
- No clipboard usage
- No window title storage
- Learned preferences store only app category, day phase, fullscreen state, luminance class, monitor id, offsets, and confidence

Content brightness analysis is opt-in. When enabled, Light Pilot samples a tiny in-memory frame and immediately reduces it to brightness aggregates. Raw pixels are not persisted.

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

## Local Install

```powershell
.\scripts\install-local.ps1
```

This installs to `%LOCALAPPDATA%\LightPilot\App`, creates a Start Menu shortcut, starts the tray app, and registers startup with `--background`.

Smoke check:

```powershell
.\scripts\smoke.ps1
```

Uninstall local app files:

```powershell
.\scripts\uninstall-local.ps1
```

## Package

```powershell
.\scripts\package-release.ps1 -Version 0.3.0
```

Build installer too when Inno Setup 6 is installed:

```powershell
.\scripts\package-release.ps1 -Version 0.3.0 -BuildInstaller
```

## License

MIT License. Copyright (c) 2026 edfpolo.
