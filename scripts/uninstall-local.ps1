$ErrorActionPreference = "Stop"
$installDir = Join-Path $env:LOCALAPPDATA "Aptema\App"
$expectedRoot = [System.IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA "Aptema"))
$installFullPath = [System.IO.Path]::GetFullPath($installDir)

if (-not $installFullPath.StartsWith($expectedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to remove unexpected path: $installFullPath"
}

Get-Process Aptema.App -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "Aptema" -ErrorAction SilentlyContinue

$shortcutPath = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Aptema.lnk"
Remove-Item -LiteralPath $shortcutPath -Force -ErrorAction SilentlyContinue

if (Test-Path -LiteralPath $installDir) {
    Remove-Item -LiteralPath $installDir -Recurse -Force
}

Write-Host "Uninstalled Aptema local app files. Legacy Light Pilot settings were preserved."
