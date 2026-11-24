# Installs and runs the packages in .\dist. Changes the machine, so it is meant for a disposable build agent.
$ErrorActionPreference = "Stop"
if (-not $env:CI) { throw "This script installs software and is only meant for CI." }

$root = Split-Path $PSScriptRoot
$dist = Join-Path $root "dist"
$app = Join-Path $env:ProgramFiles "ZerachExplorer"
$exe = Join-Path $app "ZerachExplorer.exe"
[xml]$props = Get-Content (Join-Path $root "Directory.Build.props")
$version = "$($props.Project.PropertyGroup.Version)".Trim()

function Invoke-Setup([string]$File) {
    $process = Start-Process $File "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART" -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "$File failed with exit code $($process.ExitCode)." }
}

function Assert-Starts([string]$File) {
    $process = Start-Process $File -PassThru
    for ($i = 0; $i -lt 60 -and -not $process.HasExited -and $process.MainWindowHandle -eq 0; $i++) {
        Start-Sleep -Seconds 1
        $process.Refresh()
    }
    # The normal window title names the application; an error message has another one.
    Start-Sleep -Seconds 2
    $process.Refresh()
    $title = $process.MainWindowTitle
    if ($process.HasExited -or $title -notmatch "זרח") { throw "$File did not start. Window: '$title'" }
    [void]$process.CloseMainWindow()
    if (-not $process.WaitForExit(15000)) { $process.Kill(); throw "$File did not close." }
    Write-Host "Started and closed: $File"
}

# The published 1.4.0 that users have today.
$old = Join-Path $env:RUNNER_TEMP "old-setup.exe"
Invoke-WebRequest "https://github.com/abaye123/ZerachExplorer/releases/download/1.4.0/ZerachExplorer_setop_1.4.0_signed.exe" -OutFile $old
Invoke-Setup $old
if (-not (Test-Path (Join-Path $app "LiteExplorer.dll"))) { throw "1.4.0 was not installed where expected." }

Invoke-Setup (Join-Path $dist "ZerachExplorer_setup.exe")
if (Test-Path (Join-Path $app "LiteExplorer.dll")) { throw "Files of 1.4.0 were left behind." }
$installed = (Get-Item $exe).VersionInfo.ProductVersion
if (-not $installed.StartsWith($version)) { throw "Installed version is $installed." }
$entries = @(Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*" | Where-Object { $_.PSObject.Properties["DisplayName"] -and $_.DisplayName -like "ZerachExplorer*" })
if ($entries.Count -ne 1) { throw "Expected one installed product, found $($entries.Count)." }

Assert-Starts $exe
Assert-Starts (Join-Path $dist "ZerachExplorer_portable.exe")

# Settings import, as a script would run it.
$settings = Join-Path $env:RUNNER_TEMP "settings.json"
'{"Permissions":{"CanList":true,"CanOpen":true},"AllowedExtensions":[".pdf"],"LogRetentionDays":9}' | Set-Content $settings -Encoding utf8
$import = Start-Process $exe @("--import-settings", $settings) -PassThru -Wait
if ($import.ExitCode -ne 0) { throw "Import failed with exit code $($import.ExitCode)." }
$config = Join-Path $env:ProgramData "ZerachExplorer\config.json"
if ((Get-Content $config -Raw | ConvertFrom-Json).LogRetentionDays -ne 9) { throw "Settings were not imported." }
$acl = Get-Acl $config
if ($acl.Owner -notmatch "Administrators|SYSTEM") { throw "Unexpected owner: $($acl.Owner)" }
$writable = $acl.Access | Where-Object { $_.IdentityReference -match "Users|Everyone" -and $_.FileSystemRights -match "Write|Modify|FullControl" }
if ($writable) { throw "Regular users can change the settings." }
Assert-Starts $exe

'{"unrelated":true}' | Set-Content $settings -Encoding utf8
$import = Start-Process $exe @("--import-settings", $settings) -PassThru -Wait
if ($import.ExitCode -ne 3) { throw "An unrelated file gave exit code $($import.ExitCode)." }

$crashes = Join-Path $env:LOCALAPPDATA "ZerachExplorer\logs\crash-*.log"
if (Select-String -Path $crashes -Pattern "DispatcherUnhandledException" -ErrorAction SilentlyContinue) {
    Get-Content $crashes
    throw "The application reported errors."
}

Invoke-Setup (Join-Path $app "unins000.exe")
Start-Sleep -Seconds 3
if (Test-Path $exe) { throw "The application was not removed." }
Write-Host "All packages verified."
