# Agent Context

Light Pilot is a professional adaptive screen comfort utility for Windows.

## Product Rule

One job: make screens comfortable automatically.

Do not turn the app into a dashboard. Normal UI must avoid charts, raw logs, and technical noise.

## Important Defaults

- Content brightness analysis is opt-in and local only.
- Settings live under `%LOCALAPPDATA%\LightPilot`.
- No screenshots, pixels, app content, or window titles should be persisted.
- Brightness control must tolerate unsupported monitors.
- Fullscreen games/videos/presentations should not receive disruptive changes.
- Normal UI should be human-first: avoid control-layer names, raw reasons, and percentage-heavy presentation.
- Adaptive transitions should remain gradual: brightness max 3 points per decision, warmth max 200K per decision.
- Quick Adjust feedback must store only local aggregates. Do not store window titles, screenshots, pixels, or content.
- Home must stay calm: no dense display list, no raw percentages, no DDC/WMI/overlay labels.

## Project Shape

```text
src/LightPilot.Core
src/LightPilot.Infrastructure
src/LightPilot.App
tests/LightPilot.Core.Tests
tests/LightPilot.Infrastructure.Tests
tests/LightPilot.App.Tests
docs/
```

Core should remain deterministic and OS-free. Infrastructure owns Windows APIs. App owns WPF and tray behavior.

## Validation

Run:

```powershell
dotnet build LightPilot.sln
dotnet test LightPilot.sln
```

Do not run native tests that change real monitor brightness unless the user explicitly opts in.

Local install and startup smoke:

```powershell
.\scripts\install-local.ps1
.\scripts\smoke.ps1
```
