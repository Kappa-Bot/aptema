param(
    [string]$Version = "0.4.0",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$BuildInstaller
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $artifacts "Aptema-$Version-$Runtime"
$zipPath = Join-Path $artifacts "Aptema-$Version-$Runtime.zip"
$project = Join-Path $repoRoot "src\Aptema.App\Aptema.App.csproj"
$updaterProject = Join-Path $repoRoot "src\Aptema.Updater\Aptema.Updater.csproj"
$updaterDir = Join-Path $artifacts "Aptema.Updater-$Version-$Runtime"
$manifestPath = Join-Path $artifacts "Aptema-$Version-$Runtime.manifest.json"
$artifactsFullPath = [System.IO.Path]::GetFullPath($artifacts)
$repoFullPath = [System.IO.Path]::GetFullPath($repoRoot)

if (-not $artifactsFullPath.StartsWith($repoFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to write artifacts outside repository."
}

New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $updaterDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $manifestPath -Force -ErrorAction SilentlyContinue

dotnet publish $project -c $Configuration -r $Runtime --self-contained true /p:PublishSingleFile=true -o $publishDir
dotnet publish $updaterProject -c $Configuration -r $Runtime --self-contained true /p:PublishSingleFile=true -o $updaterDir

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal
$sha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
[ordered]@{ Version = $Version; PackageFileName = [IO.Path]::GetFileName($zipPath); Sha256 = $sha256 } |
    ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding utf8
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

    $installerScript = Join-Path $repoRoot "installer\Aptema.iss"
    & $iscc.Source "/DMyAppVersion=$Version" "/DMyRuntime=$Runtime" $installerScript
}
