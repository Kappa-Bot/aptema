param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$installDir = Join-Path $env:LOCALAPPDATA "Aptema\App"
$stagingDir = Join-Path $env:LOCALAPPDATA "Aptema\install-staging"
$previousDir = Join-Path $env:LOCALAPPDATA "Aptema\App.previous"
$updaterDir = Join-Path $env:LOCALAPPDATA "Aptema\Updater"
$project = Join-Path $repoRoot "src\Aptema.App\Aptema.App.csproj"
$updaterProject = Join-Path $repoRoot "src\Aptema.Updater\Aptema.Updater.csproj"

Get-Process Aptema.App -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process LightPilot.App -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item -LiteralPath $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $stagingDir, $updaterDir -Force | Out-Null

dotnet publish $project -c $Configuration -r $Runtime --self-contained true /p:PublishSingleFile=true -o $stagingDir
dotnet publish $updaterProject -c $Configuration -r $Runtime --self-contained true /p:PublishSingleFile=true -o $updaterDir

$stagedExe = Join-Path $stagingDir "Aptema.App.exe"
$smoke = Start-Process -FilePath $stagedExe -ArgumentList "--smoke-test --safe-mode --no-hardware" -WindowStyle Hidden -Wait -PassThru
if ($smoke.ExitCode -ne 0) { throw "Aptema smoke test failed with exit code $($smoke.ExitCode)." }

Remove-Item -LiteralPath $previousDir -Recurse -Force -ErrorAction SilentlyContinue
if (Test-Path -LiteralPath $installDir) { Move-Item -LiteralPath $installDir -Destination $previousDir }
Move-Item -LiteralPath $stagingDir -Destination $installDir

$exe = Join-Path $installDir "Aptema.App.exe"
$runCommand = '"' + $exe + '" --background'
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "Aptema" -Value $runCommand
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "LightPilot" -ErrorAction SilentlyContinue

$startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$shortcutPath = Join-Path $startMenuDir "Aptema.lnk"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $installDir
$shortcut.IconLocation = $exe
$shortcut.Description = "Adaptive screen comfort for Windows"
$shortcut.Save()
Remove-Item -LiteralPath (Join-Path $startMenuDir "Light Pilot.lnk") -Force -ErrorAction SilentlyContinue

Start-Process -FilePath $exe -ArgumentList "--background" -WindowStyle Hidden
Write-Host "Installed Aptema to $installDir"
