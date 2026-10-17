param([int]$SampleSeconds = 30)
$ErrorActionPreference = 'Stop'
if ($SampleSeconds -lt 5 -or $SampleSeconds -gt 60) { throw 'Use a sample between 5 and 60 seconds.' }
$projectRoot = Split-Path -Parent $PSScriptRoot
$publishPath = Join-Path $projectRoot 'artifacts\publish\win-x64'
$executable = Join-Path $publishPath 'SensSync.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Publish SensSync first.' }
$startup = [Diagnostics.Stopwatch]::StartNew()
# This is the visible interactive app requested for review, not a background helper.
$appProcess = Start-Process -FilePath $executable -PassThru
if (-not $appProcess.WaitForInputIdle(15000)) { throw 'Application did not reach an idle message loop in 15 seconds.' }
$startup.Stop()
Start-Sleep -Seconds 3
$appProcess.Refresh()
$initialCpu = $appProcess.TotalProcessorTime.TotalMilliseconds
$sample = [Diagnostics.Stopwatch]::StartNew()
Start-Sleep -Seconds $SampleSeconds
$sample.Stop()
$appProcess.Refresh()
$result = [ordered]@{
    MeasuredAtUtc = [DateTime]::UtcNow.ToString('o')
    StartupToInputIdleMs = [Math]::Round($startup.Elapsed.TotalMilliseconds, 2)
    SampleSeconds = [Math]::Round($sample.Elapsed.TotalSeconds, 3)
    IdleCpuMilliseconds = [Math]::Round($appProcess.TotalProcessorTime.TotalMilliseconds - $initialCpu, 3)
    IdleCpuPercentOfOneCore = [Math]::Round(($appProcess.TotalProcessorTime.TotalMilliseconds - $initialCpu) / $sample.Elapsed.TotalMilliseconds * 100, 4)
    PrivateBytes = $appProcess.PrivateMemorySize64
    WorkingSetBytes = $appProcess.WorkingSet64
    PublishBytes = (Get-ChildItem -LiteralPath $publishPath -File -Recurse | Measure-Object -Property Length -Sum).Sum
    Note = 'Single local run; startup measures message-loop readiness, not visual rendering. Includes bundled runtime. App left open.'
}
$result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectRoot 'artifacts\performance.json') -Encoding utf8
$result | ConvertTo-Json
