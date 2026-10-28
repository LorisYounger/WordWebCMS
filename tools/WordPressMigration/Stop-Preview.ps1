$ErrorActionPreference = 'Stop'
$statePath = Join-Path $PSScriptRoot 'Migration-private/preview-process.json'
if (!(Test-Path -LiteralPath $statePath)) { Write-Output 'No managed preview process.'; exit 0 }
$state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
$process = Get-Process -Id $state.Id -ErrorAction SilentlyContinue
if (!$process) { Write-Output 'Preview has already stopped.'; exit 0 }
if ($process.ProcessName -ne 'dotnet' -or $process.StartTime.ToUniversalTime().ToString('o') -ne $state.StartUtc) {
    throw 'Process identity differs; refusing to stop an unrelated process.'
}
Stop-Process -Id $process.Id
Write-Output 'Preview stopped.'
