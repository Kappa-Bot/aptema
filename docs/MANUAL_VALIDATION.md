# Manual Validation

Run these checks after `dotnet build` and `dotnet test` pass.

## Launch

- Start `LightPilot.App`.
- Confirm the compact main window opens.
- Confirm the main window uses simple labels such as `Comfortable`, `Soft`, and `Adjusting gently`.
- On first foreground launch, confirm onboarding explains Balanced, Too bright/Too dim, and tray-first behavior.
- Confirm closing the window hides it instead of terminating the app.
- Confirm the tray icon remains available.

## Tray

- Left click opens the main window.
- Right click shows Auto on/off, Pause 30 min, Pause until tomorrow, Current mode, Settings, Exit.
- Pause changes the main window state.
- Resume returns to gradual automatic adjustment.
- Exit shuts down tray icon and app process.

## Settings

- Change comfort intensity and restart the app.
- Use `Too dim` and `Too bright`; confirm the comfort level changes gently.
- Confirm settings persist under `%LOCALAPPDATA%\LightPilot\settings.json`.
- Confirm content brightness analysis is off by default.
- Confirm reset restores safe defaults.
- Add a display rule with `=off`; confirm that display shows `Not controlled`.

## Detection

- Open a browser, VS Code, VLC, and a fullscreen app.
- Confirm current mode/reason updates without crashes.
- Confirm fullscreen video/game contexts avoid brightness jumps.
- Confirm warmth changes gradually rather than jumping to a very warm tone.
- On battery-powered hardware, confirm content analysis stays off while battery reduction is enabled.

## Monitor Safety

- Run on a monitor without DDC/CI support.
- Confirm the app does not crash.
- Confirm the app remains understandable without showing technical control details in the main window.

## Packaging

```powershell
.\scripts\package-release.ps1 -Version 0.2.0
```

## Installer Scripts

- Run `.\scripts\install-local.ps1`.
- Confirm app starts in the tray.
- Confirm Start Menu shortcut `Light Pilot` exists.
- Confirm startup registry uses `--background`.
- Run `.\scripts\smoke.ps1`.
- Run `.\scripts\update-local.ps1` only when network access to GitHub is expected.
