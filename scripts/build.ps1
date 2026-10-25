param(
    [ValidateSet('win-arm64','win-x64')][string]$Runtime = 'win-x64',
    [string]$Dotnet = 'dotnet',
    [string]$OutputDirectory = ''
)
$ErrorActionPreference = 'Stop'
$yutaRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $yutaRoot
try {
    if (-not $OutputDirectory) { $OutputDirectory = Join-Path $yutaRoot "release\yuta\$Runtime" }
    $OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
    & $Dotnet publish Yuta.csproj -c Release -r $Runtime --self-contained true -o $OutputDirectory -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw 'Yuta build failed.' }
    Write-Output "Ready: $OutputDirectory\Yuta.exe"
} finally { Pop-Location }
