<#
.SYNOPSIS
    Build ClickerFixer.Satellite as a self-contained, single-file
    linux-arm64 binary, copy it to the Raspberry Pi, and restart the
    service.
.DESCRIPTION
    The Pi's address is never hardcoded here (this repo is public). Set it
    either via the CLICKER_PI_HOST environment variable, or by copying
    scripts/deploy.config.ps1.example to scripts/deploy.config.ps1 (already
    gitignored) and filling in $PiHost there.
.EXAMPLE
    pwsh scripts/deploy-satellite.ps1
.EXAMPLE
    $env:CLICKER_PI_HOST = "pi@192.168.1.42"; pwsh scripts/deploy-satellite.ps1
#>
$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$LocalConfig = Join-Path $PSScriptRoot "deploy.config.ps1"

$PiHost = $env:CLICKER_PI_HOST
if (-not $PiHost -and (Test-Path $LocalConfig)) {
    . $LocalConfig
}

if ([string]::IsNullOrWhiteSpace($PiHost)) {
    Write-Error "Pi host not set. Either set `$env:CLICKER_PI_HOST = 'pi@YOUR_IP', or copy scripts/deploy.config.ps1.example to scripts/deploy.config.ps1 and set `$PiHost there (that file is gitignored)."
    exit 1
}

$RemoteDir = "/opt/clicker-fixer-app"
$Service = "clicker.service"

$Project = Join-Path $RepoRoot "ClickerFixer.Satellite\ClickerFixer.Satellite.csproj"
$RID = "linux-arm64"
$Configuration = "Release"
$OutDir = Join-Path $RepoRoot "dist\satellite"
$UnitFileLocal = Join-Path $RepoRoot "deploy\clicker.service"

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

Write-Host "==> Ensuring $Service is installed on the Pi"
# First-ever deploy to a host has no unit file yet - "systemctl stop" on a
# never-installed unit errors with "Unit ... not loaded." Install it here so
# that error can't happen; harmless/idempotent on a host that already has it.
$unitExists = (ssh $PiHost "test -f /etc/systemd/system/$Service && echo yes || echo no").Trim()
if ($unitExists -ne "yes") {
    Write-Host "    Not found on Pi - installing from $UnitFileLocal"
    scp $UnitFileLocal "${PiHost}:/tmp/$Service"
    if ($LASTEXITCODE -ne 0) { throw "scp of unit file failed with exit code $LASTEXITCODE" }
    ssh $PiHost "sudo mv /tmp/$Service /etc/systemd/system/$Service && sudo systemctl daemon-reload && sudo systemctl enable '$Service'"
    if ($LASTEXITCODE -ne 0) { throw "installing $Service failed with exit code $LASTEXITCODE" }
} else {
    Write-Host "    Already installed"
}

Write-Host "==> Stopping $Service (can't overwrite its running binary otherwise - ETXTBSY)"
# Still non-fatal: stopping a loaded-but-never-started unit is a harmless no-op.
ssh $PiHost "sudo systemctl stop '$Service'" 2>&1 | Write-Host

Write-Host "==> Copying to ${PiHost}:${RemoteDir}"
# Copies publish output into RemoteDir. app.yml (the runtime config file
# living only on the Pi) is never part of the publish output, so it is
# never touched by this copy.
ssh $PiHost "sudo mkdir -p '$RemoteDir' && sudo chown -R `$(whoami) '$RemoteDir'"
if ($LASTEXITCODE -ne 0) { throw "ssh mkdir/chown failed with exit code $LASTEXITCODE" }

# scp.exe doesn't glob-expand "*" itself on Windows (there's no shell doing
# it, unlike on Unix) - enumerate the published files/dirs explicitly instead.
$publishedItems = (Get-ChildItem -Path $OutDir).FullName
scp -r @publishedItems "${PiHost}:${RemoteDir}/"
if ($LASTEXITCODE -ne 0) { throw "scp failed with exit code $LASTEXITCODE" }

# scp from Windows never carries a Unix execute bit - Windows filesystems have
# no such concept, so the binary lands on the Pi without +x regardless of what
# it had here. Without this, both a manual run and systemd's ExecStart fail
# with "Permission denied".
Write-Host "==> Marking binary executable"
ssh $PiHost "chmod +x '$RemoteDir/ClickerFixer.Satellite'"
if ($LASTEXITCODE -ne 0) { throw "chmod failed with exit code $LASTEXITCODE" }

Write-Host "==> Starting $Service"
ssh $PiHost "sudo systemctl daemon-reload && sudo systemctl start '$Service'"
if ($LASTEXITCODE -ne 0) { throw "ssh start failed with exit code $LASTEXITCODE" }

Write-Host "==> Status"
ssh $PiHost "sudo systemctl status '$Service' --no-pager -l"

Write-Host "==> Recent logs"
ssh $PiHost "sudo journalctl -u '$Service' -n 30 --no-pager"

Write-Host "==> Done"
