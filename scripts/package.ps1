param(
    [ValidateSet('win-arm64','win-x64')][string]$Runtime = 'win-x64',
    [string]$Dotnet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = [xml](Get-Content -LiteralPath (Join-Path $repo 'Yuta.csproj') -Raw)
$version = [string]$project.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Set a semantic Version in Yuta.csproj before packaging.' }
$output = Join-Path $repo 'release\downloads'
$stage = Join-Path $repo ("release\staging\$Runtime-" + [Guid]::NewGuid().ToString('N'))
$app = Join-Path $stage 'Yuta'
New-Item -ItemType Directory -Path $output -Force | Out-Null
& (Join-Path $PSScriptRoot 'build.ps1') -Runtime $Runtime -Dotnet $Dotnet -OutputDirectory $app
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE'),(Join-Path $repo 'THIRD_PARTY_NOTICES.md') -Destination $app
Copy-Item -LiteralPath (Join-Path $repo 'docs\INSTALL.md') -Destination (Join-Path $app 'INSTALL.md')
$assets = Get-Content -LiteralPath (Join-Path $repo 'obj\project.assets.json') -Raw | ConvertFrom-Json
$licenses = Join-Path $app 'licenses'
New-Item -ItemType Directory -Path $licenses -Force | Out-Null
foreach ($notice in @(
    @('naudio/2.2.1/license.txt','NAudio.txt'),
    @('microsoft.web.webview2/1.0.4022.49/LICENSE.txt','WebView2.txt'),
    @('microsoft.web.webview2/1.0.4022.49/NOTICE.txt','WebView2-NOTICE.txt')
)) {
    $found = $false
    foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
        $source = Join-Path $folder $notice[0]
        if (Test-Path -LiteralPath $source) {
            Copy-Item -LiteralPath $source -Destination (Join-Path $licenses $notice[1])
            $found = $true
            break
        }
    }
    if (-not $found) { throw "Missing dependency notice: $($notice[0])" }
}
$deps = Get-Content -LiteralPath (Join-Path $app 'Yuta.deps.json') -Raw | ConvertFrom-Json
foreach ($pack in @($deps.libraries.PSObject.Properties.Name | Where-Object { $_ -like 'runtimepack.*' })) {
    $relative = $pack.Substring('runtimepack.'.Length).ToLowerInvariant()
    $found = $false
    foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
        $source = Join-Path $folder $relative
        if (-not (Test-Path -LiteralPath $source)) { continue }
        $texts = @(Get-ChildItem -LiteralPath $source -File | Where-Object { $_.Name -match '^(LICENSE(\.TXT)?|NOTICE(\.TXT)?|THIRD-PARTY-NOTICES(\.TXT)?)$' })
        foreach ($file in $texts) {
            $name = ($relative -replace '/', '-') + '-' + $file.Name
            Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $licenses $name)
            $found = $true
        }
        if (-not $found) {
            # Some targeting packs supply a license URL instead of a text file.
            $nuspec = Get-ChildItem -LiteralPath $source -Filter '*.nuspec' -File | Select-Object -First 1
            if ($nuspec) {
                $metadata = ([xml](Get-Content -LiteralPath $nuspec.FullName -Raw)).package.metadata
                if ([string]$metadata.licenseUrl -match '^https://') {
                    $name = ($relative -replace '/', '-') + '-LICENSE-REFERENCE.txt'
                    @($metadata.id, $metadata.copyright, "License supplied by the upstream package: $($metadata.licenseUrl)") | Set-Content -LiteralPath (Join-Path $licenses $name) -Encoding UTF8
                    $found = $true
                }
            }
        }
        if ($found) { break }
    }
    if (-not $found) { throw "Missing runtime-pack license: $pack" }
}
$commit = (& git -C $repo rev-parse HEAD)
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve the packaged source commit.' }
@{ version=$version; runtime=$Runtime; commit=$commit } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $app 'release-info.json') -Encoding UTF8
$archive = Join-Path $output "Yuta-$version-$Runtime.zip"
if (Test-Path -LiteralPath $archive) { throw "Archive already exists: $archive. Keep or move the previous package before rebuilding." }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $archive, [IO.Compression.CompressionLevel]::Optimal, $false)
$checksum = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
"$checksum  $([IO.Path]::GetFileName($archive))" | Set-Content -LiteralPath "$archive.sha256" -Encoding ASCII
Write-Output "Packaged: $archive"
