# Release Checklist

## v0.2.0

1. Run `dotnet build LightPilot.sln`.
2. Run `dotnet test LightPilot.sln`.
3. Run `.\scripts\install-local.ps1`.
4. Run `.\scripts\smoke.ps1`.
5. Run `.\scripts\package-release.ps1 -Version 0.2.0`.
6. If Inno Setup 6 exists, run `.\scripts\package-release.ps1 -Version 0.2.0 -BuildInstaller`.
7. Create tag `v0.2.0` and let GitHub Actions publish the release.

Hardware-changing native tests are opt-in only.
