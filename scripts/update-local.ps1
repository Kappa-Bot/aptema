param(
    [string]$Repository = "Kappa-Bot/light-pilot"
)

$ErrorActionPreference = "Stop"
$release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repository/releases/latest" -Headers @{ "User-Agent" = "Aptema-Updater" }
$asset = $release.assets | Where-Object { $_.name -like "Aptema-*-win-x64.zip" } | Select-Object -First 1
$manifestAsset = $release.assets | Where-Object { $_.name -eq ($asset.name -replace '\.zip$', '.manifest.json') } | Select-Object -First 1
if ($null -eq $asset -or $null -eq $manifestAsset) {
    throw "Signed update package metadata is incomplete."
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("AptemaUpdate-" + [guid]::NewGuid().ToString("N"))
$zipPath = Join-Path $tempRoot $asset.name
$manifestPath = Join-Path $tempRoot $manifestAsset.name
$installDir = Join-Path $env:LOCALAPPDATA "Aptema\App"
$updater = Join-Path $env:LOCALAPPDATA "Aptema\Updater\Aptema.Updater.exe"
if (-not (Test-Path -LiteralPath $updater)) { throw "Aptema updater is not installed." }

New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
try {
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zipPath -Headers @{ "User-Agent" = "Aptema-Updater" }
    Invoke-WebRequest -Uri $manifestAsset.browser_download_url -OutFile $manifestPath -Headers @{ "User-Agent" = "Aptema-Updater" }
    Get-Process Aptema.App -ErrorAction SilentlyContinue | Stop-Process -Force

    $update = Start-Process -FilePath $updater -ArgumentList @("--package", $zipPath, "--manifest", $manifestPath, "--install-dir", $installDir) -WindowStyle Hidden -Wait -PassThru
    if ($update.ExitCode -ne 0) { throw "Update failed safely. Exit code: $($update.ExitCode)." }

    $exe = Join-Path $installDir "Aptema.App.exe"
    $runCommand = '"' + $exe + '" --background'
    Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "Aptema" -Value $runCommand
    Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "LightPilot" -ErrorAction SilentlyContinue
    Start-Process -FilePath $exe -ArgumentList "--background" -WindowStyle Hidden
    Write-Host "Updated Aptema to $($release.tag_name)"
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
