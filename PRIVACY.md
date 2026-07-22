# Aptema Privacy

Aptema works fully offline. It has no account, telemetry, advertising, cloud sync, or screen-data service.

## Stored Locally

- settings, display preferences, profiles, application rules, and shortcuts
- process name plus selected application category; never window title
- learned aggregate context, offsets, sample count, and confidence
- sanitized diagnostic codes in bounded rotating logs

Primary data root: `%LOCALAPPDATA%\Aptema`. Legacy `%LOCALAPPDATA%\LightPilot` data is imported idempotently and never deleted automatically.

## Content Analysis

Off by default. When enabled, a tiny low-frequency sample is reduced in memory to average luminance and bright/dark ratios. Raw pixels and images are discarded immediately. Nothing is written to disk, clipboard, logs, or network.

## Support Packages

Created only after an explicit user action. Packages contain product version, health state, sanitized issue codes, and Aptema-generated diagnostic lines. They exclude screenshots, pixels, process names, window titles, settings, personal paths, and secrets.

## User Control

Pause or exit anytime. Disable content analysis, learning, DDC/CI, startup, or individual displays. Forget learned comfort or reset defaults in the app. Uninstalling Aptema does not silently delete legacy data.
