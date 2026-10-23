param(
    [ValidateSet('win-arm64','win-x64')][string]$Runtime = 'win-x64',
    [string]$Dotnet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$yutaRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $yutaRoot
try {
    & $Dotnet publish Yuta.csproj -c Release -r $Runtime --self-contained true -o "release\yuta\$Runtime" -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw 'Yuta build failed.' }
    Write-Output "Ready: $yutaRoot\release\yuta\$Runtime\Yuta.exe"
} finally { Pop-Location }
