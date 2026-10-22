param(
    [ValidateSet('Setup', 'EditMode', 'PlayMode', 'AllTests', 'Android')]
    [string]$Task = 'Setup',
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe',
    [switch]$Graphics
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not (Test-Path -LiteralPath $UnityPath)) {
    throw "Khong tim thay Unity. Truyen -UnityPath duong dan toi Unity.exe (6000.6.5f1)."
}

New-Item -ItemType Directory -Path (Join-Path $projectRoot 'Logs') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $projectRoot 'TestResults') -Force | Out-Null
$tasks = if ($Task -eq 'AllTests') { @('EditMode', 'PlayMode') } else { @($Task) }

foreach ($currentTask in $tasks) {
    $log = Join-Path $projectRoot ('Logs\' + $currentTask.ToLowerInvariant() + '.log')
    $arguments = @('-batchmode', '-projectPath', ('"' + $projectRoot + '"'), '-logFile', ('"' + $log + '"'))
    if (-not $Graphics) { $arguments += '-nographics' }
    switch ($currentTask) {
        'Setup' { $arguments += @('-executeMethod', 'Pat.ArLab.Editor.LabProjectBuilder.Setup', '-quit') }
        'Android' { $arguments += @('-executeMethod', 'Pat.ArLab.Editor.LabProjectBuilder.BuildAndroid', '-quit') }
        default {
            $result = Join-Path $projectRoot ('TestResults\' + $currentTask.ToLowerInvariant() + '.xml')
            $arguments += @('-runTests', '-testPlatform', $currentTask, '-testResults', ('"' + $result + '"'))
        }
    }
    Write-Host "Running $currentTask in $projectRoot"
    $process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WorkingDirectory $projectRoot -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        Get-Content -LiteralPath $log -Tail 65
        throw "Unity $currentTask failed (exit $($process.ExitCode)). Log: $log"
    }
    if ($currentTask -eq 'EditMode' -or $currentTask -eq 'PlayMode') {
        if (-not (Test-Path -LiteralPath $result)) { throw "Unity did not create test results: $result" }
        [xml]$report = Get-Content -LiteralPath $result -Raw
        $summary = $report.'test-run'
        Write-Host "$currentTask : result=$($summary.result), passed=$($summary.passed), failed=$($summary.failed)"
        if ($summary.result -ne 'Passed') { throw "Test failure. Open $result" }
    }
    Write-Host "OK. Log: $log"
}
