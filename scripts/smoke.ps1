$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repoRoot
try {
    dotnet build Aptema.sln
    dotnet test Aptema.sln

    $installDir = Join-Path $env:LOCALAPPDATA "Aptema\App"
    $exe = Join-Path $installDir "Aptema.App.exe"
    if (-not (Test-Path -LiteralPath $exe)) {
        throw "Installed app not found: $exe"
    }

    $before = @(Get-Process Aptema.App -ErrorAction SilentlyContinue).Count
    Start-Process -FilePath $exe -ArgumentList "--background" -WindowStyle Hidden
    Start-Sleep -Seconds 2
    $after = @(Get-Process Aptema.App -ErrorAction SilentlyContinue).Count
    if ($after -ne 1) {
        throw "Single-instance smoke failed. Before=$before After=$after"
    }

    $startup = Get-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "Aptema" -ErrorAction Stop
    if ($startup.Aptema -notlike '*--background*') {
        throw "Startup command missing --background"
    }

    Write-Host "Smoke OK"
}
finally {
    Pop-Location
}
