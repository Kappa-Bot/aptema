param(
    [string]$Repository = "Kappa-Bot/light-pilot"
)

$ErrorActionPreference = "Stop"
$release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repository/releases/latest" -Headers @{ "User-Agent" = "LightPilot-Updater" }
$asset = $release.assets | Where-Object { $_.name -like "LightPilot-*-win-x64.zip" } | Select-Object -First 1
if ($null -eq $asset) {
    throw "No win-x64 ZIP asset found in latest release."
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("LightPilotUpdate-" + [guid]::NewGuid().ToString("N"))
$zipPath = Join-Path $tempRoot $asset.name
$extractPath = Join-Path $tempRoot "extract"
$installDir = Join-Path $env:LOCALAPPDATA "LightPilot\App"

New-Item -ItemType Directory -Path $tempRoot, $extractPath -Force | Out-Null
try {
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zipPath -Headers @{ "User-Agent" = "LightPilot-Updater" }
    Expand-Archive -Path $zipPath -DestinationPath $extractPath -Force

    Get-Process LightPilot.App -ErrorAction SilentlyContinue | Stop-Process -Force
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
    Copy-Item -Path (Join-Path $extractPath "*") -Destination $installDir -Recurse -Force

    $exe = Join-Path $installDir "LightPilot.App.exe"
    $runCommand = '"' + $exe + '" --background'
    Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "LightPilot" -Value $runCommand
    Start-Process -FilePath $exe -ArgumentList "--background" -WindowStyle Hidden
    Write-Host "Updated Light Pilot to $($release.tag_name)"
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
