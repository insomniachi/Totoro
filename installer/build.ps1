<#
.SYNOPSIS
    Publishes Totoro and builds its installer (installer\output\Totoro-<version>-setup-x64.exe).

.PARAMETER Version
    The release's version, e.g. 2.1.0. Defaults to the project's.

.EXAMPLE
    ./installer/build.ps1 -Version 2.1.0
#>
param(
    [string] $Version
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'src\Totoro\Totoro.csproj'
$publish = Join-Path $PSScriptRoot 'publish'

if (-not $Version) {
    $Version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
}

# Self-contained, so it runs without installing .NET first; the Windows App SDK's runtime goes with it too (see the
# project). Not with a publish profile: those aren't in the repository.
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish $project -c Release -r win-x64 --self-contained -p:Platform=x64 -p:Version=$Version -o $publish
if ($LASTEXITCODE -ne 0) { throw "Publishing failed." }

$iscc = (Get-Command iscc.exe -ErrorAction SilentlyContinue).Source
if (-not $iscc) { $iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" }
if (-not (Test-Path $iscc)) { throw "Inno Setup 6 isn't installed: https://jrsoftware.org/isinfo.php (or: winget install JRSoftware.InnoSetup)" }

& $iscc "/DAppVersion=$Version" "/DPublishDir=$publish" (Join-Path $PSScriptRoot 'Totoro.iss')
if ($LASTEXITCODE -ne 0) { throw "Building the installer failed." }
