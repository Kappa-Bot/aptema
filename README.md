# Aptema

![Aptema mark](src/Aptema.App/Assets/Aptema-128.png)

**Every screen, in balance.**

Adaptive screen comfort for Windows. Aptema runs quietly from the system tray, makes small brightness and warmth corrections, protects fullscreen work, and learns from `Too bright`, `Too dim`, `Warmer`, `Cooler`, and `Perfect` feedback.

Private by design. Local by default.

## Product

- Tray-first daily experience plus full WPF configuration app
- DDC/CI, Windows brightness, then per-display overlay fallback
- Maximum automatic step: 3 brightness points and 200K warmth
- Fullscreen game, video, and presentation protection
- Local aggregate preference learning with confidence and explanations
- Stable multi-monitor identity, reversible display tests, onboarding
- Opt-in in-memory luminance analysis; no screenshot storage
- Recovery mode after repeated startup failures
- Sanitized bounded diagnostics and privacy-filtered support ZIP
- SHA-256 update staging, smoke test, known-good copy, rollback

## Privacy

No account, cloud, telemetry, payments, screenshot persistence, window-title storage, or content upload. Learned data contains process name/category, day phase, fullscreen state, luminance class, monitor ID, bounded offsets, and confidence only. See [PRIVACY.md](PRIVACY.md).

## Build And Test

```powershell
dotnet build Aptema.sln -c Release
dotnet test Aptema.sln -c Release --no-build
```

Run normally, in tray, or without hardware writes:

```powershell
dotnet run --project src/Aptema.App/Aptema.App.csproj
dotnet run --project src/Aptema.App/Aptema.App.csproj -- --background
dotnet run --project src/Aptema.App/Aptema.App.csproj -- --safe-mode --no-hardware
```

## Install And Package

```powershell
.\scripts\install-local.ps1
.\scripts\smoke.ps1
.\scripts\package-release.ps1 -Version 0.4.0
.\scripts\package-release.ps1 -Version 0.4.0 -BuildInstaller
```

Local install uses `%LOCALAPPDATA%\Aptema\App`, registers `Aptema --background`, creates an Aptema Start Menu shortcut, migrates existing Light Pilot settings without deleting them, and leaves a known-good application copy. Inno Setup 6 is required only for the installer.

Hardware-changing tests never run unless `APTEMA_ALLOW_HARDWARE_TESTS=1` is explicitly set.

## License

MIT License. Copyright (c) 2026 edfpolo.
