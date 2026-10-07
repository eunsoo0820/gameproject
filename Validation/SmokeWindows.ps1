$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$executable = Join-Path $workspace 'Builds/Windows-20261006/drift.exe'
$playerLog = Join-Path $PSScriptRoot 'windows-player.log'
$resultFile = Join-Path $PSScriptRoot 'windows-launch-result.txt'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build executable is missing.' }
$process = Start-Process -FilePath $executable -WorkingDirectory (Split-Path -Parent $executable) -ArgumentList @('-batchmode', '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720', '-logFile', ('"' + $playerLog + '"')) -WindowStyle Hidden -PassThru
try {
    $earlyExit = $process.WaitForExit(12000)
    if ($earlyExit) { throw "Player exited early: $($process.ExitCode)" }
    if (-not (Test-Path -LiteralPath $playerLog)) { throw 'Player did not create its startup log.' }
    $errors = @(Select-String -LiteralPath $playerLog -Pattern 'Exception:|NullReferenceException|MissingReferenceException|Shader error|Could not produce class|Failed to initialize player|Crash!!!')
    if ($errors.Count -gt 0) { throw ('Player startup errors: ' + ($errors.Line -join [Environment]::NewLine)) }
    $result = 'PASS: Windows x64 development player stayed running for 12 seconds with graphics enabled; no matching startup exceptions. Automated startup smoke test only, not manual standalone playthrough.'
    Set-Content -LiteralPath $resultFile -Value $result -Encoding utf8
    Write-Output $result
}
catch {
    Set-Content -LiteralPath $resultFile -Value $_.Exception.Message -Encoding utf8
    throw
}
finally {
    # Close only the process this script launched; never an existing Unity Editor or player.
    if (-not $process.HasExited) {
        $closed = $process.CloseMainWindow()
        if (-not $closed -or -not $process.WaitForExit(3000)) { $process.Kill(); $process.WaitForExit() }
    }
    $process.Dispose()
}
