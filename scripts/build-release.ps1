# Builds the installer and the single-file executable into .\dist.
# With -Thumbprint, everything is signed with that certificate from the user's store and verified.
param(
    [string]$Thumbprint = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot
$dist = Join-Path $root "dist"
$work = Join-Path $dist "work"
$folder = Join-Path $work "folder"
$single = Join-Path $work "single"
$timestampServer = "http://timestamp.digicert.com"

function Invoke-Checked([scriptblock]$Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code ${LASTEXITCODE}: $Command" }
}

function Find-SignTool {
    $kits = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    $tool = Get-ChildItem "$kits\10.*\x64\signtool.exe" | Sort-Object { [version]$_.Directory.Parent.Name } | Select-Object -Last 1
    if (-not $tool) { throw "signtool.exe was not found." }
    return $tool.FullName
}

function Find-InnoCompiler {
    $compiler = Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"
    if (-not (Test-Path $compiler)) { throw "Inno Setup 6 was not found." }
    return $compiler
}

function Add-Signature([string[]]$Files) {
    if (-not $Thumbprint) { return }
    Invoke-Checked { & $signTool sign /sha1 $Thumbprint /fd sha256 /tr $timestampServer /td sha256 /q $Files }
    foreach ($file in $Files) { Assert-Signature $file }
}

# The certificate is self-signed, so the status is not "Valid"; the signer and the timestamp are what matter.
function Assert-Signature([string]$File) {
    $signature = Get-AuthenticodeSignature $File
    if ($signature.SignerCertificate.Thumbprint -ne $Thumbprint) { throw "Wrong signer: $File" }
    if (-not $signature.TimeStamperCertificate) { throw "No timestamp: $File" }
    if ($signature.Status -in "HashMismatch", "NotSigned") { throw "Broken signature: $File" }
}

[xml]$props = Get-Content (Join-Path $root "Directory.Build.props")
$version = "$($props.Project.PropertyGroup.Version)".Trim()
$iscc = Find-InnoCompiler
if ($Thumbprint) { $signTool = Find-SignTool }

if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Force $folder, $single | Out-Null

$project = Join-Path $root "LiteExplorer\LiteExplorer.csproj"
$common = @("--configuration", "Release", "--runtime", "win-x64", "--self-contained", "true", "-p:DebugType=none", "--nologo")

# The installer gets plain files: a single-file executable unpacks libraries into a folder the user can write to.
Invoke-Checked { dotnet publish $project @common -p:SatelliteResourceLanguages=he -o $folder }
Invoke-Checked { dotnet publish $project @common -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $single }

# Only our own files lack a signature; the .NET runtime is already signed by Microsoft.
$unsigned = Get-ChildItem $folder -Recurse -Include *.exe, *.dll | Where-Object { (Get-AuthenticodeSignature $_.FullName).Status -eq "NotSigned" }
Add-Signature ($unsigned.FullName)

$portable = Join-Path $dist "ZerachExplorer_portable_$version.exe"
Copy-Item (Join-Path $single "ZerachExplorer.exe") $portable
Add-Signature $portable

$arguments = @("/Qp", "/DAppVersion=$version", "/DSourceDir=$folder")
if ($Thumbprint) {
    $arguments += "/DSigned"
    $arguments += "/Srelease=`$q$signTool`$q sign /sha1 $Thumbprint /fd sha256 /tr $timestampServer /td sha256 /q `$f"
}
Invoke-Checked { & $iscc @arguments (Join-Path $root "installer\ZerachExplorer.iss") }

$setup = Join-Path $dist "ZerachExplorer_setup_$version.exe"
if ($Thumbprint) { Assert-Signature $setup }

Get-ChildItem $dist -File -Filter *.exe | ForEach-Object {
    "$((Get-FileHash $_.FullName -Algorithm SHA256).Hash)  $($_.Name)"
} | Set-Content (Join-Path $dist "checksums.sha256") -Encoding ascii

Remove-Item $work -Recurse -Force
Get-ChildItem $dist -File | Select-Object Name, Length
