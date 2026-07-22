# Aptema Project Decisions

## Stack And Boundaries

- C#, .NET 10, WPF, xUnit, JSON; no SQLite or cloud
- `Aptema.Core`: deterministic OS-free policy, learning, profiles, rules
- `Aptema.Application`: use cases, immutable runtime snapshots, coordinators
- `Aptema.Infrastructure`: Windows APIs, persistence, diagnostics, startup
- `Aptema.App`: WPF, tray, OSD, overlays, onboarding, composition only
- `Aptema.Updater`: separate transactional updater; no self-replacement

ViewModels never poll, call native APIs, write monitors, or persist directly. Latest-value channels collapse stale context/luminance events.

## Safety

Automatic changes remain within 3 brightness points and 200K per decision, cooldown, hysteresis, user limits, and monitor limits. Manual feedback remains bounded. Fullscreen protection suppresses automatic writes. DDC failures degrade one display only. Recovery mode disables DDC, overlays, hotkeys, and content analysis.

## Persistence And Migration

Schema 6 settings use atomic replacement, three known-good backups, corruption quarantine, and future-schema preservation. Stable displays prefer DisplayConfig path plus EDID, then WMI, then legacy alias. Light Pilot v3 import is idempotent and never removes source data.

## Updates

Update packages require a matching SHA-256 manifest. Aptema extracts into `App.next`, blocks path traversal, runs a safe smoke test, retains `App.previous`, switches, runs a second smoke test, and rolls back on failure. This verifies integrity, not publisher authenticity; code signing remains required for that.
