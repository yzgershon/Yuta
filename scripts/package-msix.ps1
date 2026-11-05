param(
    [Parameter(Mandatory=$true)][string]$PayloadDirectory,
    [Parameter(Mandatory=$true)][ValidatePattern('^\d+\.\d+\.\d+\.\d+$')][string]$Version,
    [Parameter(Mandatory=$true)][ValidateSet('arm64','x64')][string]$Architecture,
    [string]$Publisher = 'CN=Yuta',
    [string]$CertificateThumbprint = '',
    [string]$SdkBin = ''
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$payload = (Resolve-Path -LiteralPath $PayloadDirectory).Path
if (-not (Test-Path -LiteralPath (Join-Path $payload 'Yuta.exe'))) { throw 'Supply an extracted, tested portable Yuta package.' }
if (-not (Test-Path -LiteralPath (Join-Path $payload 'release-info.json'))) { throw 'Use the package.ps1 payload with release metadata and license notices.' }
$release = Get-Content -LiteralPath (Join-Path $payload 'release-info.json') -Raw | ConvertFrom-Json
if ($release.runtime -ne "win-$Architecture") { throw 'The payload architecture does not match the requested MSIX architecture.' }
if (-not $SdkBin) {
    $kit = Get-ChildItem -LiteralPath "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Directory |
        Where-Object { $_.Name -match '^10\.0\.\d+\.0$' } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    if (-not $kit) { throw 'Install the Windows 10/11 SDK packaging tools first.' }
    $SdkBin = Join-Path $kit.FullName 'x64'
}
$makeAppx = Join-Path $SdkBin 'makeappx.exe'
if (-not (Test-Path -LiteralPath $makeAppx)) { throw 'MakeAppx.exe was not found in SdkBin.' }
$stage = Join-Path $repo ('artifacts\msix-' + [Guid]::NewGuid().ToString('N'))
$output = Join-Path $repo 'release\msix'
New-Item -ItemType Directory -Path $stage,$output -Force | Out-Null
# Only package a curated payload. No profile, checkout history, or test captures.
foreach ($entry in Get-ChildItem -LiteralPath $payload) {
    if ($entry.Name -match '^(state\.json|clipboard\.bin|self-test\.json|performance\.json|smoke|.*-renders)$') { continue }
    Copy-Item -LiteralPath $entry.FullName -Destination $stage -Recurse
}
$manifest = [xml](Get-Content -LiteralPath (Join-Path $repo 'packaging\AppxManifest.xml') -Raw)
$manifest.Package.Identity.SetAttribute('Publisher',$Publisher)
$manifest.Package.Identity.SetAttribute('Version',$Version)
$manifest.Package.Identity.SetAttribute('ProcessorArchitecture',$Architecture)
$manifest.Save((Join-Path $stage 'AppxManifest.xml'))
Add-Type -AssemblyName System.Drawing
$assets = Join-Path $stage 'Assets'; New-Item -ItemType Directory -Path $assets -Force | Out-Null
$icon = New-Object Drawing.Icon((Join-Path $stage 'ui\assets\yuta.ico'),256,256)
$source = $icon.ToBitmap()
try {
    foreach ($asset in @(@('StoreLogo.png',50),@('Square44x44Logo.png',44),@('Square150x150Logo.png',150))) {
        $bitmap = New-Object Drawing.Bitmap($source,[Drawing.Size]::new($asset[1],$asset[1]))
        try { $bitmap.Save((Join-Path $assets $asset[0]),[Drawing.Imaging.ImageFormat]::Png) } finally { $bitmap.Dispose() }
    }
} finally { $source.Dispose(); $icon.Dispose() }
$package = Join-Path $output "Yuta-$Version-$Architecture.msix"
if (Test-Path -LiteralPath $package) { throw "Package already exists: $package" }
& $makeAppx pack /d $stage /p $package
if ($LASTEXITCODE -ne 0) { throw 'MSIX validation or packaging failed.' }
if ($CertificateThumbprint) {
    $signTool = Join-Path $SdkBin 'signtool.exe'
    & $signTool sign /fd SHA256 /sha1 $CertificateThumbprint $package
    if ($LASTEXITCODE -ne 0) { throw 'MSIX signing failed. The certificate subject must match Publisher.' }
    & $signTool verify /pa $package
    if ($LASTEXITCODE -ne 0) { throw 'MSIX signature verification failed.' }
    Write-Output "Signed package: $package"
} else { Write-Output "Unsigned validation artifact: $package (not ready for end-user installation)" }
Get-FileHash -LiteralPath $package -Algorithm SHA256
