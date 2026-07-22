# Aptema Agent Context

Product rule: make screens comfortable automatically. Keep normal UI calm, human, and tray-first. No dashboards, charts, raw logs, technical reason codes, or primary-view monitor percentages.

Architecture boundaries are strict: Core deterministic/OS-free; Application coordinates use cases; Infrastructure owns Windows/persistence; App owns WPF/tray/composition. Never move timers, native calls, monitor writes, or direct persistence into ViewModels.

Privacy invariants: no cloud, telemetry, screenshots, raw pixels, window titles, typed content, or content logs. Process names may be stored only for explicit app categorization. Content analysis stays opt-in and in-memory.

Safety invariants: minimum limits, 3-point/200K automatic steps, cooldown, hysteresis, fullscreen protection, per-display failure isolation, pause/reset, no physical monitor tests without `APTEMA_ALLOW_HARDWARE_TESTS=1`.

Validation:

```powershell
dotnet build Aptema.sln -c Release
dotnet test Aptema.sln -c Release --no-build
.\scripts\smoke.ps1
```

Current projects: `src/Aptema.Core`, `src/Aptema.Application`, `src/Aptema.Infrastructure`, `src/Aptema.App`, `src/Aptema.Updater`, and matching tests plus `Aptema.Integration.Tests`.
