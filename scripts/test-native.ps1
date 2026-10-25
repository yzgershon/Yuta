param(
    [ValidateSet('win-arm64','win-x64')][string]$Runtime = 'win-x64',
    [ValidateSet('self-test','performance-test','smoke-test')][string]$Mode = 'self-test',
    [string]$Directory = ''
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $Directory) { $Directory = Join-Path $repo "release\yuta\$Runtime" }
$directory = [IO.Path]::GetFullPath($Directory)
$exe = Join-Path $directory 'Yuta.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw "Build $Runtime first with scripts/build.ps1." }
$testProcess = Start-Process -FilePath $exe -ArgumentList "--$Mode" -WorkingDirectory $directory -WindowStyle Hidden -PassThru
if (-not $testProcess.WaitForExit(90000)) {
    Stop-Process -Id $testProcess.Id
    throw "$Mode exceeded 90 seconds."
}
if ($testProcess.ExitCode -ne 0) { throw "$Mode exited with code $($testProcess.ExitCode)." }
switch ($Mode) {
    'self-test' {
        $checks = Get-Content -LiteralPath (Join-Path $directory 'self-test.json') -Raw | ConvertFrom-Json
        if ($checks.Count -eq 0 -or @($checks | Where-Object {-not $_.passed}).Count) { throw 'Native fixtures failed.' }
        Write-Output "$($checks.Count) native fixtures passed."
    }
    'performance-test' {
        $result = Get-Content -LiteralPath (Join-Path $directory 'performance.json') -Raw | ConvertFrom-Json
        if (-not $result.checks.Count -or @($result.checks | Where-Object {-not $_.passed}).Count) { throw 'Native interaction checks failed.' }
        Write-Output "$($result.checks.Count) native interaction checks passed."
    }
    'smoke-test' {
        foreach ($section in @('home','today','stats','wallpaper','routines','settings')) {
            $layout = Get-Content -LiteralPath (Join-Path $directory "smoke\$section.json") -Raw | ConvertFrom-Json | ConvertFrom-Json
            if ($layout.overflow -or $layout.scrollNeeded -or @($layout.errors).Count) { throw "Layout failed: $section" }
        }
        $cold = Get-Content -LiteralPath (Join-Path $directory 'smoke\cold-resume.json') -Raw | ConvertFrom-Json
        if ($cold.suspended -or -not $cold.expanded -or -not $cold.webVisible) { throw 'Cold resume failed.' }
        Write-Output 'Six panels and cold resume passed.'
    }
}
