# Shift Jam: upload a build to the private itch.io playtest page with butler (D-0123).
#
#   Right-click this file -> Run with PowerShell, or in Windows Terminal:
#     powershell -ExecutionPolicy Bypass -File "docs\tools\itch_push.ps1"          (the newest ShiftJam_vN share copy)
#     powershell -ExecutionPolicy Bypass -File "docs\tools\itch_push.ps1" -Version 10
#
# Needs butler in C:\butler (https://itchio.itch.io/butler, Windows 64-bit, extracted there). The first run asks
# you to log in: butler opens your browser, you approve, and it remembers you after that.
# It pushes Builds\Windows\ShiftJam_vN (the share copy without LLMUnity's libraries) to
# hyphysaurus/shift-jam-playtest:windows, with the game's own version number. Friends with the itch app get it
# as a small patch: only the changed files.

param([int]$Version = 0)

$ErrorActionPreference = 'Stop'
$butler  = 'C:\butler\butler.exe'
$target  = 'hyphysaurus/shift-jam-playtest:windows'
$project = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)   # docs\tools -> the project folder
$builds  = Join-Path $project 'Builds\Windows'

if (-not (Test-Path $butler)) {
    Write-Host "butler is not in C:\butler. Get it from https://itchio.itch.io/butler (Windows 64-bit) and extract it there." -ForegroundColor Yellow
    Start-Process 'https://itchio.itch.io/butler'
    Read-Host 'Press Enter to close'
    exit 1
}

if ($Version -le 0) {
    $newest = Get-ChildItem $builds -Directory -Filter 'ShiftJam_v*' |
        Where-Object { $_.Name -match '^ShiftJam_v(\d+)$' } |
        Sort-Object { [int]($_.Name -replace '^ShiftJam_v', '') } |
        Select-Object -Last 1
    if (-not $newest) { Write-Host "No Builds\Windows\ShiftJam_vN folder found." -ForegroundColor Red; Read-Host 'Press Enter to close'; exit 1 }
    $Version = [int]($newest.Name -replace '^ShiftJam_v', '')
}

$folder = Join-Path $builds ("ShiftJam_v" + $Version)
if (-not (Test-Path (Join-Path $folder 'ShiftJam.exe'))) {
    Write-Host "$folder has no ShiftJam.exe." -ForegroundColor Red
    Read-Host 'Press Enter to close'
    exit 1
}

# Logged in? butler keeps its key in your user folder after the first login.
$creds = Join-Path $env:USERPROFILE '.config\itch\butler_creds'
if (-not (Test-Path $creds)) {
    Write-Host 'First time: butler will open your browser to log in to itch.io. Approve it, then come back here.' -ForegroundColor Cyan
    & $butler login
}

Write-Host "Uploading v$Version ($folder) to $target ..." -ForegroundColor Cyan
& $butler push $folder $target --userversion $Version
if ($LASTEXITCODE -ne 0) { Write-Host "butler push failed (exit $LASTEXITCODE)." -ForegroundColor Red; Read-Host 'Press Enter to close'; exit $LASTEXITCODE }

& $butler status $target
Write-Host "Done: v$Version is on https://hyphysaurus.itch.io/shift-jam-playtest (itch processes it for a minute or two)." -ForegroundColor Green
Read-Host 'Press Enter to close'
