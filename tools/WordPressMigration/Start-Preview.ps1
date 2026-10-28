$ErrorActionPreference = 'Stop'
$statePath = Join-Path $PSScriptRoot 'Migration-private/preview-process.json'
if (Test-Path -LiteralPath $statePath) {
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    $existing = Get-Process -Id $state.Id -ErrorAction SilentlyContinue
    if ($existing -and $existing.StartTime.ToUniversalTime().ToString('o') -eq $state.StartUtc) {
        Write-Output 'Preview is already running: http://127.0.0.1:5197'
        exit 0
    }
}
if (Get-NetTCPConnection -LocalPort 5197 -State Listen -ErrorAction SilentlyContinue) {
    throw 'Port 5197 is in use. Stop the existing listener or choose a different preview port.'
}
$logRoot = Join-Path $PSScriptRoot 'Migration-private'
$dllArgument = '"' + (Join-Path $PSScriptRoot 'WordWebCMS.dll') + '"'
$process = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @($dllArgument, '--urls', 'http://127.0.0.1:5197') `
    -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput (Join-Path $logRoot 'preview.stdout.log') -RedirectStandardError (Join-Path $logRoot 'preview.stderr.log')
@{ Id = $process.Id; StartUtc = $process.StartTime.ToUniversalTime().ToString('o') } | ConvertTo-Json | Set-Content -LiteralPath $statePath
Write-Output "Preview process started (PID $($process.Id)): http://127.0.0.1:5197"
