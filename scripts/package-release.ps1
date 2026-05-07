param(
    [string]$Version = "0.2.1",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$BuildInstaller
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $artifacts "LightPilot-$Version-$Runtime"
$zipPath = Join-Path $artifacts "LightPilot-$Version-$Runtime.zip"
$project = Join-Path $repoRoot "src\LightPilot.App\LightPilot.App.csproj"
$artifactsFullPath = [System.IO.Path]::GetFullPath($artifacts)
$repoFullPath = [System.IO.Path]::GetFullPath($repoRoot)

if (-not $artifactsFullPath.StartsWith($repoFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to write artifacts outside repository."
}

New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue

dotnet publish $project -c $Configuration -r $Runtime --self-contained true /p:PublishSingleFile=true -o $publishDir

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal
Write-Host $zipPath

if ($BuildInstaller) {
    $iscc = Get-Command "iscc.exe" -ErrorAction SilentlyContinue
    if ($null -eq $iscc) {
        $defaultInno = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
        if (Test-Path -LiteralPath $defaultInno) {
            $iscc = [pscustomobject]@{ Source = $defaultInno }
        }
    }

    if ($null -eq $iscc) {
        throw "Inno Setup compiler not found. Install Inno Setup 6 or run without -BuildInstaller."
    }

    $installerScript = Join-Path $repoRoot "installer\LightPilot.iss"
    & $iscc.Source "/DMyAppVersion=$Version" "/DMyRuntime=$Runtime" $installerScript
}
