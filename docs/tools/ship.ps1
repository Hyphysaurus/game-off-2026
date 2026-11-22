# Shift Jam: ship the next version to the private itch.io playtest page in one go (D-0127).
#
#   Close Unity first (it builds without the editor), then right-click this file -> Run with PowerShell.
#   Or in Windows Terminal, in the project folder:
#     powershell -ExecutionPolicy Bypass -File docs\tools\ship.ps1             build the next version, check it, upload it
#     powershell -ExecutionPolicy Bypass -File docs\tools\ship.ps1 -NoUpload   build and check; upload nothing
#     powershell -ExecutionPolicy Bypass -File docs\tools\ship.ps1 -Existing   check and upload the newest build already made
#     powershell -ExecutionPolicy Bypass -File docs\tools\ship.ps1 -Status     which version itch has live now
#   -Existing -Version 12 picks that build instead; -NoPause skips "Press Enter" at the end (when a script runs it).
#
# Four steps. It stops at the first that fails, so nothing reaches itch unless every check before it passed:
#   1. Build   Unity, headless and at low priority, rebuilds the online pieces (PartyBuilder) and builds the next
#              version into Builds\Windows\ShiftJam_vN, a folder holding the game only (BuildTools).
#   2. Check   the game plays every scene headless (-smoketest) and must log no errors.
#   3. Upload  butler (C:\butler, logged in once) pushes the folder to hyphysaurus/shift-jam-playtest:windows as
#              version N; friends' itch apps then download only the files that changed.
#   4. Live    it waits until itch has processed the build, when the itch apps can update to it.
# Logs go to Builds\Windows\logs (Unity's log, the build report, the check's log); ship-log.txt there gets a line a run.

param(
    [switch]$NoUpload,
    [switch]$Existing,
    [int]$Version = 0,
    [switch]$Status,
    [switch]$NoPause
)

$butler  = 'C:\butler\butler.exe'
$target  = 'hyphysaurus/shift-jam-playtest:windows'
$channel = 'windows'
$project = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)   # docs\tools -> the project folder
$builds  = Join-Path $project 'Builds\Windows'
$logs    = Join-Path $builds 'logs'
$clock   = [Diagnostics.Stopwatch]::StartNew()
New-Item -ItemType Directory -Force -Path $logs | Out-Null

function Say([string]$text, [string]$colour = 'Gray') { Write-Host $text -ForegroundColor $colour }

# The end of every run, good or bad: a line in ship-log.txt, the verdict, then exit with its code.
function Finish([int]$code, [string]$text) {
    $line = '{0}  {1}  ({2:0.0} min)' -f (Get-Date -Format 'yyyy-MM-dd HH:mm'), $text, $clock.Elapsed.TotalMinutes
    Add-Content -LiteralPath (Join-Path $logs 'ship-log.txt') -Value $line
    if ($code -eq 0) { Say $text 'Green' } else { Say $text 'Red' }
    if (-not $NoPause) { [void](Read-Host 'Press Enter to close') }
    exit $code
}

# Runs a program hidden at low priority and waits up to $minutes. Returns its exit code, or -1 when it ran out of time.
function RunLow([string]$exe, [string]$arguments, [int]$minutes, [string]$workDir) {
    $p = Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory $workDir -PassThru -WindowStyle Hidden
    $null = $p.Handle   # hold the handle, or ExitCode can come back empty
    try { $p.PriorityClass = 'BelowNormal' } catch {}
    if (-not $p.WaitForExit($minutes * 60 * 1000)) { try { $p.Kill() } catch {}; return -1 }
    return $p.ExitCode
}

# The channel's live build from butler's JSON status: id, state (processing, completed, failed) and userVersion.
function LiveBuild {
    foreach ($line in (& $butler --json status $target 2>$null)) {
        if ("$line" -notmatch '"type":"result"') { continue }
        foreach ($c in ("$line" | ConvertFrom-Json).value.channels) { if ($c.name -eq $channel) { return $c.head } }
    }
    return $null
}

Say "Shift Jam ship  ($project)" 'Cyan'

# Can it upload at all? Checked before the minutes of building.
if (-not $NoUpload) {
    if (-not (Test-Path -LiteralPath $butler)) {
        Finish 1 'Not started: butler is not in C:\butler. Download it from https://itchio.itch.io/butler (Windows 64-bit) and extract it there.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $env:USERPROFILE '.config\itch\butler_creds'))) {
        if ($NoPause) { Finish 1 'Not started: butler is not logged in. Run this script once by hand: it opens the itch.io login.' }
        Say 'First time: butler opens your browser to log in to itch.io. Approve it there, then come back here.' 'Yellow'
        & $butler login
        if ($LASTEXITCODE -ne 0) { Finish 1 'Not started: the itch.io login did not finish.' }
    }
}

if ($Status) {
    $head = LiveBuild
    if (-not $head) { Finish 1 "Could not read $target from itch (offline, or butler not logged in?)." }
    Finish 0 ("itch has v{0} on {1} (build #{2}, {3})." -f $head.userVersion, $target, $head.id, $head.state)
}

# 1. Build
if ($Existing) {
    if ($Version -le 0) {
        $newest = Get-ChildItem -LiteralPath $builds -Directory | Where-Object { $_.Name -match '^ShiftJam_v(\d+)$' } |
            Sort-Object { [int]($_.Name -replace '^ShiftJam_v', '') } | Select-Object -Last 1
        if (-not $newest) { Finish 1 'Not started: there is no Builds\Windows\ShiftJam_vN build yet.' }
        $Version = [int]($newest.Name -replace '^ShiftJam_v', '')
    }
    $folder = Join-Path $builds "ShiftJam_v$Version"
    if (-not (Test-Path -LiteralPath (Join-Path $folder 'ShiftJam.exe'))) { Finish 1 "Not started: $folder has no ShiftJam.exe." }
    Say "1/4  Build: using the one already made, v$Version" 'Cyan'
} else {
    # The editor this project is on (ProjectSettings\ProjectVersion.txt), where Unity Hub installs it.
    $editorVersion = ''
    $found = Select-String -LiteralPath (Join-Path $project 'ProjectSettings\ProjectVersion.txt') -Pattern '^m_EditorVersion:\s*(\S+)' | Select-Object -First 1
    if ($found) { $editorVersion = $found.Matches[0].Groups[1].Value }
    $unity = "C:\Program Files\Unity\Hub\Editor\$editorVersion\Editor\Unity.exe"
    if (-not $editorVersion -or -not (Test-Path -LiteralPath $unity)) { Finish 1 "Not started: Unity $editorVersion is not installed at $unity." }

    # Unity cannot build a project another Unity has open: the editor, or a build already running.
    $leaf = [regex]::Escape((Split-Path $project -Leaf))
    $busy = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -match $leaf -and $_.CommandLine -notmatch 'serve' })
    if ($busy.Count -gt 0) { Finish 1 "Not started: Unity has the project open (process $($busy[0].ProcessId)). Close Unity and run this again: it builds without the editor." }

    # BuildTools takes the next whole number after the one in the player settings; so does this.
    $found = Select-String -LiteralPath (Join-Path $project 'ProjectSettings\ProjectSettings.asset') -Pattern '^\s*bundleVersion:\s*(\d+)\s*$' | Select-Object -First 1
    $Version = if ($found) { [int]$found.Matches[0].Groups[1].Value + 1 } else { 1 }
    $folder   = Join-Path $builds "ShiftJam_v$Version"
    $report   = Join-Path $logs "ShiftJam_v$Version-build-report.txt"
    $unityLog = Join-Path $logs "ShiftJam_v$Version-unity.log"
    Remove-Item -LiteralPath $report -ErrorAction SilentlyContinue

    Say "1/4  Build: v$Version, Unity headless at low priority (a few minutes)..." 'Cyan'
    $env:UNITY_MCP_DISABLE_BATCH = '1'
    $code = RunLow $unity "-batchmode -projectPath `"$project`" -logFile `"$unityLog`" -executeMethod ShiftJam.PartyBuilder.RebuildAndBuildBatch" 30 $project
    $first = ''
    if (Test-Path -LiteralPath $report) { $first = Get-Content -LiteralPath $report -TotalCount 1 }
    if ($code -ne 0 -or $first -notmatch 'Succeeded') {
        $why = if ($code -eq -1) { 'it ran over 30 minutes and was stopped' } else { "Unity exit $code" }
        Finish 1 "FAILED: the v$Version build did not work ($why). Logs: $report and $unityLog"
    }
    Get-Content -LiteralPath $report | Where-Object { $_ -match '^(Time |LLMUnity)' } | ForEach-Object { Say "     $_" }
}

# 2. Check
$smokeLog = Join-Path $logs "ShiftJam_v$Version-smoke.log"
Remove-Item -LiteralPath $smokeLog -ErrorAction SilentlyContinue
Say "2/4  Check: v$Version plays every scene headless..." 'Cyan'
$code = RunLow (Join-Path $folder 'ShiftJam.exe') "-batchmode -smoketest -logFile `"$smokeLog`"" 5 $folder
$scenes = @(); $done = $null
if (Test-Path -LiteralPath $smokeLog) {
    $scenes = @(Select-String -LiteralPath $smokeLog -Pattern '\[smoke\] scene ')
    $done = Select-String -LiteralPath $smokeLog -Pattern '\[smoke\] DONE: (\d+) scenes, (\d+) errors' | Select-Object -Last 1
}
foreach ($s in $scenes) { Say ('     ' + ($s.Line -replace '^.*\[smoke\] ', '')) }
if (-not $done) {
    $why = if ($code -eq -1) { 'it ran over 5 minutes and was stopped' } else { "it ended early, exit $code" }
    Finish 1 "FAILED: the v$Version check did not finish ($why); nothing uploaded. Log: $smokeLog"
}
$errors  = [int]$done.Matches[0].Groups[2].Value
$checked = '{0} scenes, {1} errors' -f $done.Matches[0].Groups[1].Value, $errors
if ($errors -gt 0 -or $code -ne 0) { Finish 1 "FAILED: v$Version logged errors in its check ($checked); nothing uploaded. Log: $smokeLog" }
if ($NoUpload) { Finish 0 "v$Version built and checked ($checked); not uploaded (-NoUpload): $folder" }

# 3. Upload (in this window, so butler's progress shows)
Say "3/4  Upload: v$Version to $target (only the changed files)..." 'Cyan'
$push = Start-Process -FilePath $butler -ArgumentList "push `"$folder`" $target --userversion $Version" -NoNewWindow -Wait -PassThru
if ($push.ExitCode -ne 0) { Finish 1 "FAILED: the v$Version upload did not work (butler exit $($push.ExitCode))." }

# 4. Live
Say "4/4  Live: waiting for itch to process v$Version (usually a minute or two)..." 'Cyan'
$until = (Get-Date).AddMinutes(10)
while ((Get-Date) -lt $until) {
    Start-Sleep -Seconds 15
    $head = LiveBuild
    if ($head -and "$($head.userVersion)" -eq "$Version") {
        if ($head.state -eq 'completed') { Finish 0 "v$Version is LIVE on itch (build #$($head.id); check: $checked). Friends' itch apps update by themselves." }
        if ($head.state -eq 'failed') { Finish 1 "FAILED: itch could not process v$Version (build #$($head.id))." }
    }
}
Finish 1 "v$Version is uploaded, but itch had not finished processing it after 10 minutes. Look again with: ship.ps1 -Status"
