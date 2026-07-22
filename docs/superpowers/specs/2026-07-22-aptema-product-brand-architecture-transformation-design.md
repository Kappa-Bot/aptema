# Aptema product, brand, and architecture specification

## Product contract

Aptema is an adaptive screen-comfort assistant for Windows. Daily use is tray-first; the full application handles setup, displays, rules, learning, privacy, diagnostics, and updates.

- Brand: **Aptema**
- Promise: **Every screen, in balance.**
- Descriptor: **Adaptive screen comfort for Windows**
- Privacy: **Private by design. Local by default.**
- Runtime: fully local, no account, cloud, telemetry, payments, or captured-content persistence.

## Architecture

- Core contains deterministic comfort, profile, fatigue, classification, and learning logic. It has no Windows or persistence dependencies.
- Application owns use cases, coordination, runtime snapshots, typed results, cancellation, and event flow.
- Infrastructure owns Windows APIs, persistence, migration, updates, logging, and packaging adapters.
- App owns WPF presentation, navigation, accessibility, tray flyout, and OSD. ViewModels do not poll, persist, or call monitor APIs.

All long-running work is cancellable. Context and luminance use bounded latest-value delivery. A failed display degrades independently. Tests never write real hardware unless `APTEMA_ALLOW_HARDWARE_TESTS=1`.

## Experience

Home answers four questions: is the screen comfortable, what changed, why, and how to correct or pause it. Advanced surfaces are Displays, Applications, Profiles, Automation, Learning, Privacy, Diagnostics, Updates, and Settings.

Quick feedback is Too bright, Too dim, Warmer, Cooler, and Perfect. Feedback changes remain safety-clamped, update aggregate learning only, and expose a ten-second undo. Fullscreen games, videos, and presentations stay protected.

## Data and compatibility

Final data root is `%LOCALAPPDATA%\Aptema`. Migration from `%LOCALAPPDATA%\LightPilot` is idempotent and never deletes legacy data. Writes are atomic and retain a known-good backup. Configuration evolves through schemas 4, 5, and 6.

Display identity prefers DisplayConfig target path plus EDID, then WMI identity, with legacy display names retained as aliases. Learning stores only normalized process/category/time/display/luminance aggregates.

## Delivery gates

Every release must build without new warnings, pass all tests, pass no-hardware smoke tests, and preserve pause/reset. Final delivery includes portable ZIP, Inno Setup installer, local startup installation, rollback-capable updater, current screenshots, privacy documentation, and no remote publish/tag/release.

