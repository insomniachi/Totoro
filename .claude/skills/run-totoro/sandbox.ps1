# Launches Totoro in Windows Sandbox as a fresh install, with the ReAnime plugin installed and posters working.
# Usage: pwsh .claude/skills/run-totoro/sandbox.ps1   (prints the app's PID last)
$ErrorActionPreference = 'Stop'
$repo = Resolve-Path "$PSScriptRoot\..\..\.."
$plugin = 'ReAnime'

# The plugin's package, from the catalog Totoro installs from, checked against the catalog's hash.
$work = Join-Path ([IO.Path]::GetTempPath()) "totoro-sandbox-$plugin"
Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $work | Out-Null
$entry = (Invoke-RestMethod 'https://raw.githubusercontent.com/insomniachi/Totoro.Plugins/main/plugins.json') | Where-Object name -eq $plugin
$package = Join-Path $work "$plugin.zip"
Invoke-WebRequest $entry.downloadUrl -OutFile $package
if ($entry.sha256 -and (Get-FileHash $package -Algorithm SHA256).Hash -ne $entry.sha256) { throw "$plugin package hash doesn't match the catalog." }
Write-Host "$plugin $($entry.version) downloaded."

# Into the guest: WinINet's revocation check hangs there (plain HTTP over IPv4 gets no reply on this network), which
# leaves every image blank; and the plugin goes where Totoro installs plugins, before it starts, so no restart is needed.
$pushed = winapp target push sandbox $package "Setup\$plugin.zip" --json | ConvertFrom-Json
if (-not $pushed.targetPath) { throw "Couldn't copy the plugin into the sandbox." }
$setup = @"
Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings' -Name CertificateRevocation -Value 0 -Type DWord
`$dir = Join-Path `$env:LOCALAPPDATA 'Totoro\Plugins\$plugin'
# winapp run replaces a running Totoro anyway; stopped first, so it doesn't hold the plugin's files.
Get-Process Totoro -ErrorAction SilentlyContinue | Stop-Process -Force -PassThru | Wait-Process
Remove-Item `$dir -Recurse -Force -ErrorAction SilentlyContinue
Expand-Archive '$($pushed.targetPath)' `$dir -Force
'$plugin installed.'
"@
winapp target exec sandbox -- powershell -NoProfile -Command $setup
if ($LASTEXITCODE) { throw 'Sandbox setup failed.' }

winapp run "$repo\src\Totoro\Totoro.csproj" --on sandbox --detach --devtools off --quiet
if ($LASTEXITCODE) { throw 'winapp run failed.' }
Start-Sleep 10
$window = winapp ui list-windows --on sandbox 2>&1 | Select-String 'Totoro, PID (\d+)'
if (-not $window) { throw 'Totoro has no window in the sandbox.' }
$window.Matches[0].Groups[1].Value
