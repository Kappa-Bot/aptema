# Diagnostics, installer, updater, rename, and release

1. Add safe mode disabling DDC, overlays, hotkeys, and content analysis, including automatic recovery after repeated startup failure.
2. Add bounded local logs, sanitized diagnostics, and privacy-filtered support bundles.
3. Build helper updater with SHA-256 manifest validation, side-by-side staging, smoke launch, known-good retention, and rollback.
4. Rename projects, assemblies, executable, mutex, events, paths, scripts, installer copy, and documentation to Aptema only after product surfaces stabilize.
5. Preserve Inno Setup AppId; migrate startup/shortcuts only after healthy Aptema launch; never delete legacy settings automatically.
6. Produce `0.4.0` ZIP and installer locally. Do not push, tag, publish, or rename remote without later authorization.
7. Gate: clean install, v0.3 upgrade, startup, tray, GUI, migration, updater failure, and rollback smoke tests all pass.
