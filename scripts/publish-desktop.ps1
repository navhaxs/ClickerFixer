<#
.SYNOPSIS
    Build and publish ClickerFixer.Desktop as a self-contained, single-file
    win-x64 executable, zipped for distribution.
.EXAMPLE
    pwsh scripts/publish-desktop.ps1
#>
$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $RepoRoot "ClickerFixer.Desktop\ClickerFixer.Desktop.csproj"
$RID = "win-x64"
$Configuration = "Release"
$OutDir = Join-Path $RepoRoot "dist\desktop"
$Version = Get-Date -Format "yyyyMMdd-HHmmss"
$DistDir = Join-Path $RepoRoot "dist"
$ZipPath = Join-Path $DistDir "ClickerFixer.Desktop-$RID-$Version.zip"

Write-Host "==> Cleaning $OutDir"
if (Test-Path $OutDir) { Remove-Item -Recurse -Force $OutDir }
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

Write-Host "==> Publishing $Project ($RID, self-contained, single-file)"
dotnet publish $Project `
    -c $Configuration `
    -r $RID `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $OutDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

Write-Host "==> Zipping to $ZipPath"
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
if (Test-Path $ZipPath) { Remove-Item -Force $ZipPath }
Compress-Archive -Path (Join-Path $OutDir "*") -DestinationPath $ZipPath

Write-Host "==> Done"
Write-Host "Published output: $OutDir"
Write-Host "Zip:              $ZipPath"
