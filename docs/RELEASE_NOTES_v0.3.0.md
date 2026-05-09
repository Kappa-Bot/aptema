# Light Pilot v0.3.0

## Premium Adaptive Assistant

- Redesigned the main window into Comfort Home, Quick Adjust, and Settings.
- Added tray feedback commands: Too bright, Too dim, and Perfect.
- Added local contextual preference learning with confidence scoring.
- Added learned decision source and confidence metadata to adaptive decisions.
- Added safe manual corrections for brightness and warmth.
- Suppressed automatic visible changes in fullscreen game/video/presentation contexts.
- Added high-contrast and balanced luminance classification.
- Added Creative app category mappings.
- Added per-monitor brightness apply results for fallback, degraded, throttled, protected, disabled, and failed states.
- Added App tests for tray menu order and tooltip behavior.

## Privacy

- No screenshots stored.
- No cloud usage.
- No telemetry.
- Learned preferences store aggregates only.

## Validation

```powershell
dotnet build LightPilot.sln
dotnet test LightPilot.sln
.\scripts\install-local.ps1
.\scripts\smoke.ps1
.\scripts\package-release.ps1 -Version 0.3.0
```
