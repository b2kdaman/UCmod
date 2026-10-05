param([Parameter(Mandatory=$true)][string]$GamePath)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$originalHash = '56A1A25A8472640344915E6CF2A385A2955AD1F50B977586DACD88E12ADDD453'
function Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
function SafePath([string]$Root,[string]$Relative) {
    if ([IO.Path]::IsPathRooted($Relative) -or $Relative -match '(^|[\\/])\.\.([\\/]|$)') { throw "Unsafe relative path: $Relative" }
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    $path = [IO.Path]::GetFullPath((Join-Path $prefix $Relative))
    if (!$path.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Path escaped its root.' }
    return $path
}
$root = (Resolve-Path -LiteralPath $GamePath).Path
if (!(Test-Path -LiteralPath (Join-Path $root 'uc.exe') -PathType Leaf)) { throw 'Choose the Umbrella Corps folder containing uc.exe.' }
foreach ($process in @(Get-Process uc -ErrorAction SilentlyContinue)) {
    if (!$process.Path -or $process.Path -eq (Join-Path $root 'uc.exe')) { throw 'Close Umbrella Corps before installing.' }
}
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Raw | ConvertFrom-Json
foreach ($file in $manifest.files) {
    $source = SafePath $PSScriptRoot $file.path
    if (!(Test-Path -LiteralPath $source -PathType Leaf) -or (Hash $source) -ne $file.sha256) { throw "Package verification failed: $($file.path)" }
}
$payload = Join-Path $PSScriptRoot 'payload'
$runtime = SafePath $root 'uc_Data/Mono/mono.dll'
$backup = SafePath $root 'uc_Data/Mono/mono.original.dll'
$loaderHash = Hash (SafePath $payload 'uc_Data/Mono/mono.dll')
if (!(Test-Path -LiteralPath $runtime -PathType Leaf)) { throw 'Game runtime is missing.' }
$currentHash = Hash $runtime
if ($currentHash -ne $originalHash -and $currentHash -ne $loaderHash) { throw 'Unsupported or modified Mono runtime. No files were changed.' }
if (Test-Path -LiteralPath $backup) {
    if ((Hash $backup) -ne $originalHash) { throw 'Existing original-runtime backup failed verification. No files were changed.' }
} elseif ($currentHash -ne $originalHash) { throw 'The mod loader is present but its original-runtime backup is missing.' }
$items = @($manifest.files | Where-Object { $_.path.StartsWith('payload/') })
foreach ($item in $items) {
    $relative = $item.path.Substring(8)
    $destination = SafePath $root $relative
    if ($relative -in @('uc_Data/Mono/mono.dll','UltrawideFix/settings.ini')) { continue }
    if ((Test-Path -LiteralPath $destination) -and (Hash $destination) -ne $item.sha256) {
        throw "Existing file differs: $relative. Back it up and remove it before installing; no files were changed."
    }
}
# Install the loader last so an interrupted first install leaves the original runtime active.
if (!(Test-Path -LiteralPath $backup)) { Copy-Item -LiteralPath $runtime -Destination $backup }
if ((Hash $backup) -ne $originalHash) { throw 'Backup verification failed.' }
foreach ($item in $items) {
    $relative = $item.path.Substring(8)
    if ($relative -eq 'uc_Data/Mono/mono.dll') { continue }
    $destination = SafePath $root $relative
    if ($relative -eq 'UltrawideFix/settings.ini' -and (Test-Path -LiteralPath $destination)) { continue }
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath (SafePath $PSScriptRoot $item.path) -Destination $destination -Force
    if ((Hash $destination) -ne $item.sha256) { throw "Installed file verification failed: $relative" }
}
New-Item -ItemType Directory -Path (SafePath $root 'UltrawideFix/diagnostics') -Force | Out-Null
try {
    Copy-Item -LiteralPath (SafePath $payload 'uc_Data/Mono/mono.dll') -Destination $runtime -Force
    if ((Hash $runtime) -ne $loaderHash) { throw 'Loader verification failed.' }
} catch {
    Copy-Item -LiteralPath $backup -Destination $runtime -Force
    throw
}
Write-Host 'Installed Umbrella Corps Ultrawide + Toggle ADS 0.1.0-beta.1.'
Write-Host 'Settings: UltrawideFix\settings.ini. Existing settings were preserved.'
Write-Host 'Launch normally. The fix activates approximately 25 seconds after startup.'
Write-Host 'To disable/remove: close the game and run UltrawideFix\Restore-Original.ps1.'
