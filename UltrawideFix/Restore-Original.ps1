$ErrorActionPreference = 'Stop'
$gameRoot = Split-Path -Parent $PSScriptRoot
foreach ($process in @(Get-Process uc -ErrorAction SilentlyContinue)) {
    if (!$process.Path -or $process.Path -eq (Join-Path $gameRoot 'uc.exe')) {
        throw 'Close Umbrella Corps before restoring the original runtime.'
    }
}
$backupPath = Join-Path $gameRoot 'uc_Data\Mono\mono.original.dll'
$runtimePath = Join-Path $gameRoot 'uc_Data\Mono\mono.dll'
$expectedHash = '56A1A25A8472640344915E6CF2A385A2955AD1F50B977586DACD88E12ADDD453'
if ((Get-FileHash -LiteralPath $backupPath -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'Backup verification failed. Nothing was changed.'
}
Copy-Item -LiteralPath $backupPath -Destination $runtimePath -Force
if ((Get-FileHash -LiteralPath $runtimePath -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'Restoration verification failed.'
}
Write-Host 'Original Mono runtime restored. Camera/UI and toggle ADS plugins will no longer load.'
Write-Host 'Select a native resolution in the game PC settings if needed.'
